module Omnibus

open System
open Microsoft.VisualBasic.FileIO
open FSharpPlus

let join (arr : string seq) = String.Join(", ", arr)

let parseCsv (path : string) =
    use csvParser = new TextFieldParser(path)
    csvParser.SetDelimiters([| "," |])
    csvParser.HasFieldsEnclosedInQuotes <- true
    
    let result =
        seq {
            while not csvParser.EndOfData do
                yield csvParser.ReadFields()
        } |> Seq.toList
    result

type Config = {
    Workflow : (string * string) list
    InProgress : string Set
}

type Status = {
    State: string
    Date: DateTime
}

module Status =
    let date (status : Status) = status.Date
    let state (status : Status) = status.State
    let create (state : string) (date : string) =
        {
            State = state
            Date = DateTime.Parse(date) // TODO: Can throw
        }

let glueStatuses (config : Config) (statuses : Status list) =
    seq {
        let mutable lastStart : DateTime option = None
        for before, after in Seq.pairwise statuses do
            match config.InProgress.Contains before.State, config.InProgress.Contains after.State, lastStart with
            | false, true, None ->
                lastStart <- Some after.Date
            | true, false, Some lastStartValue ->
                yield after.Date - lastStartValue
                lastStart <- None
            | true, false, None -> // only first iteration
                yield after.Date - before.Date
            | _ -> () // ignore - glue adjacent segments together
    }

let cycleTime config (statuses : Status list) =
    statuses
    |> glueStatuses config
    |> Seq.map ((+) (TimeSpan.FromDays 1))
    |> sum
    |> max (TimeSpan.FromDays 1)
    
[<EntryPoint>]
let main (args : string array) =
    let config = {
        Workflow = [
            "Ready", "In Progress"
            "In Progress", "Pending Review"
            "Pending Review", "In Review"
            "In Review", "Merge & environment QA"
            "Merge & environment QA", "Ready for release"
            "Ready for release", "Done"
            
            "Ready", "Archived"
            "In Progress", "Archived"
            "Pending Review", "Archived"
            "In Review", "Archived"
            "Merge & environment QA", "Archived"
            "Ready for release", "Archived"
        ]
        InProgress = Set [
            "In Progress"
            "Pending Review"
            "In Review"
            "Merge & environment QA"
            "Ready for release"
        ]
    }

    let path = head args
    printfn "Ticket ID,Cycle Time V3,Cycle Time (since first),Cycle Time (since last)"
    parseCsv path |> Seq.tail |> Seq.map (fun line ->
        let ticketNo :: _status :: _daysInCC :: _ticketType :: _priority :: _component :: _epicKey :: _summary :: _date :: _flagged :: _label :: _storyPoints :: _createdDate :: statuses = line |> Array.toList
        let statuses =
            statuses
            |> List.chunkBySize 2
            |> Seq.map (function [state; date] -> Status.create state date | _ -> failwith "Unreachable")
            |> toList
        let maxCycleTime =
            monad' {
                let! start = statuses |> tryFind (Status.state >> (=) "In Progress") |> Option.map Status.date
                let! finish = statuses |> tryLast |> Option.map(Status.date)
                return finish.Subtract(start)
            }
            |> Option.defaultValue (TimeSpan.FromDays 0)
            |> (+) (TimeSpan.FromDays 1)
        let minCycleTime =
            monad' {
                let! start = statuses |> Seq.tryFindBack (Status.state >> (=) "In Progress") |> Option.map Status.date
                let! finish = statuses |> tryLast |> Option.map(Status.date)
                return finish.Subtract(start)
            }
            |> Option.defaultValue (TimeSpan.FromDays 0)
            |> (+) (TimeSpan.FromDays 1)
        ticketNo, cycleTime config statuses, maxCycleTime, minCycleTime
    )
    |> sortBy (fun (_, ct, _, _) -> ct)
    |> iter (fun (ticketNo, cycleTime, maxCycleTime, minCycleTime) ->
        printfn $"{ticketNo},{cycleTime.TotalDays},{maxCycleTime.Days},{minCycleTime.Days}"
    )
    0
