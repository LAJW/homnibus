module Homnibus

open System
open System.IO
open System.Text.RegularExpressions
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

let pickInProgressAndLastOnGivenDate (config: Config) (statuses : Status seq) : Status seq =
    statuses
    |> Seq.chunkBy Status.date
    |> Seq.map snd
    |> Seq.collect (fun group ->
        let last = group |> Seq.last
        match group |> Seq.tryFind (Status.state >> config.InProgress.Contains) with
        | Some status when status = last -> [status]
        | Some status -> [status; last]
        | None -> [last]
    )

let glueStatuses (config : Config) (statuses : Status list) =
    seq {
        let mutable lastStart =
            statuses
            |> tryHead
            |> filter (Status.state >> config.InProgress.Contains)
            |> Option.map Status.date
        for before, after in statuses |> pickInProgressAndLastOnGivenDate config |> Seq.pairwise do
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

let maxCycleTime (config : Config) statuses =
    let mutable start : DateTime option = None
    config
    |> Config.stateOrder
    |> Seq.map(fun x -> x.Key, x.Value)
    |> sortBy snd
    |> Seq.map fst
    |> filter config.InProgress.Contains
    |> takeWhile(fun _ -> start.IsNone)
    |> iter(fun state ->
        start <-
            statuses
            |> Seq.tryFind (Status.state >> (=) state)
            |> Option.map Status.date
    )
    monad {
        let! start = start
        let! finish = statuses |> tryLast |> Option.map(Status.date)
        return finish.Subtract(start)
    }
    |> Option.defaultValue (TimeSpan.FromDays 0)
    |> (+) (TimeSpan.FromDays 1)

let minCycleTime (config : Config) statuses : TimeSpan =
    let mutable start : DateTime option = None
    config
    |> Config.stateOrder
    |> Seq.map(fun x -> x.Key, x.Value)
    |> sortBy snd
    |> Seq.map fst
    |> filter config.InProgress.Contains
    |> takeWhile(fun _ -> start.IsNone)
    |> iter(fun state ->
        start <-
            statuses
            |> Seq.tryFindBack (Status.state >> (=) state)
            |> Option.map Status.date
    )
    monad {
        let! start = start
        let! finish = statuses |> tryLast |> Option.map(Status.date)
        return finish - start
    }
    |> Option.defaultValue (TimeSpan.FromDays 0)
    |> (+) (TimeSpan.FromDays 1)

let internal regex = Regex("(\"|,|[^\",]+)")

let lex (s: string) =
    regex.Matches(s) |> Seq.collect (fun m -> m.Captures) |> Seq.map (fun c -> c.Value)

let excelStyleCsvParse (row: string) =
    let tokens = row |> lex |> Seq.toArray
    let mutable index = 0
    seq {
        if tokens.Length = 0 || tokens[0] = "," then yield Ok ""
        while index < tokens.Length do
            let token = tokens[index]
            match token, (tokens |> tryItem (index + 1)) with
            | "\"", _ ->
                index <- index + 1
                let mutable word = ""
                let mutable stop = false
                if tokens |> Array.tryItem index = Some("\"") then
                    yield Ok("")
                else
                    while index < tokens.Length && not stop do
                        let token = tokens[index]
                        let nextToken = tokens |> Array.tryItem (index + 1)
                        let nextNextToken = tokens |> Array.tryItem (index + 2)
                        match nextToken, nextNextToken with
                        | Some "\"", Some "," ->
                            yield Ok(word + token)
                            stop <- true
                        | Some "\"", None ->
                            yield Ok(word + token)
                            stop <- true
                        | None, None -> yield Error "Malformed input"
                        | _, _ -> word <- word + token
                        index <- index + 1
            | ",", (Some "," | None) -> yield Ok ""
            | ",", _ -> ()
            | word, _ -> yield Ok word
            index <- index + 1
    }
    |> Seq.fold (fun result cell -> monad {
        let! result = result
        let! cell = cell
        return result @ [ cell ]
    }) (Ok [])

let consoleReadAllLines() =
     let input = Console.In
     seq {
         let mutable line = input.ReadLine()
         while line <> null do
           yield line
           line <- input.ReadLine()
     }

let sequenceResults (results : Result<'a, 'b> seq) : Result<'a list, 'b list> =
    (Ok [], results) ||> Seq.fold (fun result item ->
        match result, item with
        | Ok result, Ok state -> Ok(result @ [state])
        | Ok _, Error error -> Error [error]
        | Error errors, Error error -> Error(errors @ [error])
        | Error errors, Ok _ -> Error errors
    )
     
let processLine (config : Config) (allStates : string Set) (lineNumber : int) (rawLine : string) = monad {
    let! line = excelStyleCsvParse rawLine
    let! ticketNo, statuses =
        match line with
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
        |> sequenceResults
        |> Result.mapError (fun errors ->
            let summary = errors |> Seq.distinct |> join ", "
            $"Line {lineNumber}: {ticketNo} - {summary}"
        )
    {|
        TicketNo = ticketNo
        CycleTime = cycleTime config statuses
        MaxCycleTime = maxCycleTime config statuses
        MinCycleTime = minCycleTime config statuses
        Skips = countSkips config statuses
        ProcessViolations = countProcessViolations config statuses
    |}
}

[<EntryPoint>]
let main (args : string array) =
    let config = {
        Workflow = [
            // Here we encode allowed transitions
            // "state before", "state after"
            // Legacy transitions
            "To be refined", "Backlog"
            "Backlog", "Ready"
            "To be refined", "Triage"
            "Triage", "Ready"
            
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
    
    monad' {
        do! Config.validate config
        let allStates = Config.allStates config
        let input, maybeOutputFilePath =
            match toList args with
            | inputFilePath :: outputFilePath :: _ -> File.ReadLines inputFilePath, Some outputFilePath
            | inputFilePath :: _ -> File.ReadLines inputFilePath, None
            | [ ] -> consoleReadAllLines(), None
        let results =
            input
            |> Seq.tail
            |> Seq.mapi (fun index -> processLine config allStates (index + 2))
            |> Seq.toList

        let output =
            results
            |> Seq.choose Result.toOption
            |> sortBy (fun stats -> stats.CycleTime)
            |> Seq.map (fun stats ->
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
            )
            |> Seq.append ["Ticket ID,Cycle Time V3,Cycle Time (since first),Cycle Time (since last),Process Violations,Skips,Pushbacks"]
        
        match maybeOutputFilePath with
        | Some outputFilePath -> File.WriteAllLines(outputFilePath, output)
        | None -> output |> iter (printfn "%s")
        
        let errors = results |> Seq.choose (function Ok _ -> None | Error err -> Some err)
        
        if errors |> Seq.isEmpty |> not then
            Console.Error.WriteLine("\nErrors:")
            for error in errors do Console.Error.WriteLine(error)
        
        if Seq.isEmpty output then do! Error("No successful rows have been processed")
    }
    |> function
        | Ok () -> 0
        | Error message ->
            printfn $"{message}"
            1
