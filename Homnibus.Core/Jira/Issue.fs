module Homnibus.Core.Jira.Issue
open Homnibus.Core
open Homnibus.Core.Jira
open FSharpPlus
open System

let changeLogItems (changeLog: ChangeLog option) =
    changeLog
    |> Option.bind (fun log -> log.histories)
    |> Option.toList
    |> Seq.concat
    |> Seq.bind (fun x -> x.items)

let changeLogStatuses(changeLog: ChangeLog option) : DomainModel.Status list =
    // TODO: Maybe add flags here
    changeLog
    |> Option.bind (fun logs -> logs.histories)
    |> Option.defaultValue List.empty
    |> Seq.bind (fun history -> history.items |> Seq.map (fun item -> history, item))
    |> Seq.filter (fun (_, item) -> item.field.Contains("status"))
    |> Seq.map (fun (history, item) -> {
        Date = history.created |> parseDate
        Name = item.changedString |> Option.orElse item.fromString |?? ""
    })
    |> Seq.toList

let isFlagged(changeLog: ChangeLog option): bool =
    (false, changeLogItems changeLog) ||> Seq.fold(fun state item ->
        if item.field.Contains "Flagged" && item.changedString = Some "Impediment" then true
        elif item.fromString = Some "Impediment" && item.changedString = Some "" then false
        else state
    )

let storyPoints(changeLog: ChangeLog option): int option =
    changeLogItems changeLog
    |> Seq.rev
    |> Seq.filter (fun item -> item.field.Contains "Story Points")
    |> Seq.tryPick (fun item -> item.changedString)
    |> Option.bind Int32.tryParse

let daysInColumn(issue : Issue) : int =
    let currentStatus = issue.fields.status |> map name |?? ""
    monad {
        let! log = issue.changelog
        let! histories = log.histories
        histories |> Seq.collect (fun history ->
            let createdOnDate = parseDate history.created
            let duration = DateTime.Now - createdOnDate
            let daysInColumn = duration.Days
            history.items
            |> Seq.filter (fun item -> item.field.Contains "status")
            |> Seq.map (fun item -> daysInColumn, item)
        ) 
        |> Seq.map(fun (a, b) -> (a, b.changedString |> Option.orElse b.fromString |?? ""))
        |> Seq.filter(fun (_, b) -> b = currentStatus)
        |> Seq.map fst
        |> Seq.sum
    } |?? 0

let extract (issue : Issue) =
    {
        TicketNo = issue.key
        Status = issue.fields.status |> map name
        DaysInColumn = issue |> daysInColumn
        TicketType = issue.fields.IssueType |> map name
        Priority = issue.fields.priority |> map name
        Components = issue.fields.components |> map (List.map name) |?? List.empty
        Summary = issue.fields.summary
        Flagged = issue.changelog |> isFlagged
        Labels = issue.fields.labels |> Option.defaultValue List.empty
        StoryPoints = issue.changelog |> storyPoints
        CreatedDate = issue.fields.created |> parseDate
        Statuses = issue.changelog |> changeLogStatuses
    }
