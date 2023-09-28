open System.Net.Http
open System.Text.Json
open FSharpPlus
open System
open Homnibus.Core.Jira

let searchUrl (maxResults: int) (searchUrl: string) : string =
    searchUrl + $"&expand=changelog&maxResults={maxResults}"

let surround (ch : char) (str : string) = $"{ch}{str}{ch}"
let join (delim : string) (strings : string seq) = String.Join(delim, strings)

[<EntryPoint>]
let main (args : string array) =
    match args with
    | [| jiraHostUrl; token; filterId |] ->
        let maxResults = 10_000
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
                let issue = issue |> Issue.extract
                let ticketNo = issue.TicketNo
                let status = issue.Status |?? ""
                let daysInCC = issue.DaysInColumn |> string
                let ticketType = issue.TicketType |?? ""
                let priority = issue.Priority |?? ""
                let component_ = issue.Components |> join ", "
                let epicKey = ""
                let summary = issue.Summary |?? ""
                let date = DateTime.Now |> DateTime.toString dateOutFormat
                let flagged = if issue.Flagged then "Flagged" else ""
                let label = issue.Labels |> join ", "
                let storyPoints = issue.StoryPoints |> map string |?? ""
                let createdDate = issue.CreatedDate |> DateTime.toString dateOutFormat
                let statuses = issue.Statuses |> List.collect (fun status -> [status.Date.ToString(dateOutFormat); status.Name])
                let row = [ticketNo; status; daysInCC; ticketType; priority; component_; epicKey; summary; date; flagged; label; storyPoints; createdDate] @ statuses
                row |> map (surround '"') |> join "," |> printfn "%s"
        }
        |> Async.AwaitTask
        |> Async.RunSynchronously
        0
    | _ ->
        printfn "Bad arguments, use ./JiraExtractor.exe <Jira host URL> <jira token> <filter id>"
        1
