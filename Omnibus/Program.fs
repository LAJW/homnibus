module Omnibus

open System
open System.Collections.Generic
open Microsoft.VisualBasic.FileIO
open FSharpPlus

let join (delim : string) (arr : string seq) = String.Join(delim, arr)

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

module Config =
    let internal lhs (this : Config) = this.Workflow |> Seq.map fst |> Set
    let internal rhs (this : Config) = this.Workflow |> Seq.map snd |> Set
    
    let endStatuses (this : Config) : string Set =
        lhs this |> Set.difference (rhs this)

    let startStatuses (this : Config) : string Set =
        rhs this |> Set.difference (lhs this)

    let allStates this = lhs this |> Set.union (rhs this)

    let validate (this : Config) : Result<unit, string> =
        let allStates = allStates this
        let ends = endStatuses this
        
        monad {
            match this.InProgress |> Seq.tryFind (allStates.Contains >> not) with
            | Some unknownStatus ->
                do! Error($"Status: '{unknownStatus}' is not defined in transitions")
            | None -> ()
            match ends |> Seq.tryFind this.InProgress.Contains with
            | Some endStatus ->
                let joined = ends |> toArray |> join ", "
                do! Error($"Status: '{endStatus}' is marked as 'in progress'. End statuses are not allowed to be marked as 'in progress'. End statuses: {joined}")
            | None -> ()
        }
        
    let stateOrder (config : Config) =
        let starts = startStatuses config
        let result = Dictionary<string, int>()
        let mutable layer = starts |> toList
        let mutable order = 1
        while not layer.IsEmpty do
            for status in layer do result[status] <- order
            layer <- layer |> List.collect (fun status -> config.Workflow |> filter (fst >> (=) status) |> List.map snd)
            order <- order + 1
        result

type Status = {
    State: string
    Date: DateTime
}

module Status =
    let date (status : Status) = status.Date
    let state (status : Status) = status.State
    let create (state : string) (date : string) =
        match DateTime.TryParse date with
        | true, date ->
            Ok {
                State = state
                Date = date
            }
        | false, _ ->
            Error($"Invalid date: {date}")

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

let countProcessViolations (config : Config) (statuses : Status list) =
    let workflow = config.Workflow |> Set
    statuses |> Seq.map Status.state |> Seq.pairwise |> filter (workflow.Contains >> not) |> Seq.length

let countSkips config (statuses : Status list) =
    let workflow = Set config.Workflow
    let stateOrder = config |> Config.stateOrder
    statuses
    |> Seq.map Status.state
    |> Seq.pairwise
    |> Seq.filter (fun (before, after) ->
        stateOrder.ContainsKey before
        && stateOrder.ContainsKey after
        && stateOrder[before] < stateOrder[after]
        && workflow.Contains (before, after) |> not
    )
    |> Seq.length
    
let countPushbacks config statuses =
    (countProcessViolations config statuses) - (countSkips config statuses)

[<EntryPoint>]
let main (args : string array) =
    let config = {
        Workflow = [
            // Here we encode allowed transitions
            // "state before", "state after"
            "To be refined", "Refined"
            "Refined", "Ready"
            "Ready", "In Progress"
            "In Progress", "Pending Review"
            "Pending Review", "In Review"
            "In Review", "Merge & environment QA"
            "Merge & environment QA", "Ready for Release"
            "Ready for Release", "Done"
            
            "To be refined", "Archived"
            "Refined", "Archived"
            "Ready", "Archived"
            "In Progress", "Archived"
            "Pending Review", "Archived"
            "In Review", "Archived"
            "Merge & environment QA", "Archived"
            "Ready for Release", "Archived"
        ]
        InProgress = Set [
            "In Progress"
            "Pending Review"
            "In Review"
            "Merge & environment QA"
            "Ready for Release"
        ]
    }
    
    let enumerate = Seq.zip (Seq.initInfinite id)
    
    monad {
        do! Config.validate config
        let allStates = Config.allStates config
        let! path = args |> tryHead |> Option.toResultWith "Missing input file name"
        printfn "Ticket ID,Cycle Time V3,Cycle Time (since first),Cycle Time (since last),Process Violations,Skips,Pushbacks"
        let results = parseCsv path |> enumerate |> Seq.tail |> Seq.map (fun (index, line) -> monad {
            let! ticketNo, statuses =
                match Array.toList line with
                | ticketNo :: _status :: _daysInCC :: _ticketType :: _priority :: _component :: _epicKey :: _summary :: _date :: _flagged :: _label :: _storyPoints :: _createdDate :: statuses ->
                    Ok(ticketNo, statuses)
                | _ -> Error "Not enough elements in a row"
            let! statuses =
                statuses
                |> List.chunkBySize 2
                |> Seq.map (function
                    [state; date] ->
                        monad {
                            let! status = Status.create state date
                            if not (allStates.Contains status.State) then
                                do! Error $"Unknown state {status.State}"
                            status
                        }
                    | _ -> failwith "Unreachable")
                |> Seq.fold (fun (maybeResult : Result<Status list, string list>) maybeStatus ->
                    match maybeResult, maybeStatus with
                    | Ok result, Ok state -> Ok(result @ [state])
                    | Ok _, Error error -> Error [error]
                    | Error errors, Error error -> Error(errors @ [error])
                    | Error errors, Ok _ -> Error errors
                ) (Ok [])
                |> Result.mapError (fun errors ->
                    let summary = errors |> Seq.distinct |> join ", "
                    $"Line {index + 1}: {ticketNo} - {summary}"
                )
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
            {|
                TicketNo = ticketNo
                CycleTime = cycleTime config statuses
                MaxCycleTime = maxCycleTime
                MinCycleTime = minCycleTime
                Skips = countSkips config statuses
                ProcessViolations = countProcessViolations config statuses
            |}
        })
        let successes = results |> Seq.choose Result.toOption

        successes
        |> sortBy (fun stats -> stats.CycleTime)
        |> iter (fun stats ->
            [
                stats.TicketNo
                stats.CycleTime.Days.ToString()
                stats.MaxCycleTime.Days.ToString()
                stats.MinCycleTime.Days.ToString()
                stats.Skips.ToString()
                stats.ProcessViolations.ToString()
                (stats.ProcessViolations - stats.Skips).ToString()
            ]
            |> join ","
            |> printfn "%s"
        )
        
        let errors = results |> Seq.choose (function Ok _ -> None | Error err -> Some err)
        
        printfn "\nErrors:"
        for error in errors do printfn "%s" error
        
        if successes |> Seq.isEmpty then do! Error("No successful rows have been processed")
    }
    |> function
        | Ok () -> 0
        | Error message ->
            printfn $"{message}"
            1
