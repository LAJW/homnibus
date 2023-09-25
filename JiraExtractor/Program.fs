open System.Net.Http
open System.Text.Json
open System.Text.Json.Serialization
open FSharpPlus
open System

let searchUrl (maxResults: int) (searchUrl: string) : string =
    searchUrl + $"&expand=changelog&maxResults={maxResults}"

let inline name(self : ^T when ^T : (member name : ^V)) = (^T : (member name : ^V) self)

[<AutoOpen>]
module DataModel =
    type FilterModel = { searchUrl : string }
    type Priority = { name : string }
    type IssueType = { name : string }
    type Status = { name : string }
    type Component = { name : string }

    type Fields = {
        priority: Priority option
        labels: string list option
        status: Status option
        components: Component list option
        [<JsonPropertyName("issuetype")>]
        IssueType: IssueType option
        summary: string option
        created: string
    }

    type HistoryItem = {
        field: string
        fromString: string option
        changedString: string option
    }

    type History = {
        created : string
        items : HistoryItem list
    }

    type ChangeLog = {
        histories: History list option
    }

    type Issue = {
        key : string
        fields : Fields
        changelog : ChangeLog option
    }

    type SearchModel = {
        maxResults : int
        total : int
        startAt : int
        issues : Issue list
    }

// "2022-03-03T08:38:02.000+0000"
let dateFormat = "yyyy-MM-dd'T'HH:mm:ss.SSSZ"
let parseDate (str : string) : DateTime = DateTime.Parse(str) // TODO: Try using exact
let dateOutFormat = "dd-MM-yyyy"

let surround (ch : char) (str : string) = $"{ch}{str}{ch}"
let join (delim : string) (strings : string seq) = String.Join(delim, strings)

module DateTime =
    let toString (format : string) (date : DateTime) = date.ToString(format)

let changeLogStatuses(changeLog: ChangeLog option) =
    // TODO: Maybe add flags here
    changeLog
    |> Option.bind (fun logs -> logs.histories)
    |> Option.defaultValue []
    |> Seq.bind (fun history -> history.items |> Seq.map (fun item -> history, item))
    |> Seq.filter (fun (_, item) -> item.field.Contains("status"))
    |> Seq.map (fun (history, item) ->
        let date = history.created |> parseDate |> DateTime.toString "dd-MM-yyyy"
        let statusName = item.changedString |> Option.orElse(item.fromString) |> Option.defaultValue ""
        (statusName, date)
    )
    |> Seq.toList

[<EntryPoint>]
let main (args : string array) =
    match args with
    | [| jiraHostUrl; token; filterId |] ->
        let maxResults = 100
        let filterUrl (filterID: string) = $"{jiraHostUrl}/rest/api/2/filter/{filterID}"
        let url = filterUrl(filterId)

        task {
            use client = new HttpClient()
            let fetch (url : string) =
                task {
                    let request = new HttpRequestMessage(HttpMethod.Get, url)
                    request.Headers.Add("Authorization", $"Bearer {token}") // TODO: Error handling bad auth / 404
                    let! response = client.SendAsync(request)
                    return! response.Content.ReadAsStringAsync()
                }
            let! body = fetch url
            let filter = JsonSerializer.Deserialize<FilterModel> body // TODO: Can be null
            let! body = filter.searchUrl |> searchUrl maxResults |> fetch
            let search = JsonSerializer.Deserialize<SearchModel> body
            // "Ticket No","Status","Days In Column","Ticket Type","Priority","Components","Epic Key","Summary","Date","Flagged","Label","Story Points","Created Date","Previous Statuses"
            let header = """Ticket No","Status","Days In Column","Ticket Type","Priority","Components","Epic Key","Summary","Date","Flagged","Label","Story Points","Created Date","Previous Statuses"""
            printfn $"\"%s{header}\""
            for issue in search.issues do
                let ticketNo = issue.key
                let status = issue.fields.status |> map name |> Option.defaultValue ""
                let daysInCC = ""
                let ticketType = ""
                let priority = ""
                let component_ = ""
                let epicKey = ""
                let summary = issue.fields.summary |> Option.defaultValue ""
                let date = ""
                let flagged = ""
                let label = ""
                let storyPoints = ""
                let createdDate = issue.fields.created |> parseDate |> DateTime.toString dateOutFormat
                let statuses = issue.changelog |> changeLogStatuses |> List.collect (fun (a, b) -> [a; b])
                let row = [ticketNo; status; daysInCC; ticketType; priority; component_; epicKey; summary; date; flagged; label; storyPoints; createdDate] @ statuses
                row |> map (surround '"') |> join "," |> printfn "%s"
        }
        |> Async.AwaitTask
        |> Async.RunSynchronously
        0
    | _ ->
        printfn "Bad arguments, use ./JiraExtractor.exe <Jira host URL> <jira token> <filter id>"
        1
