open System.Net
open System.Net.Http
open System.Text.Json
open FSharpPlus
open System
open Homnibus.Core.Jira
open System.Threading.Tasks

let searchUrl (maxResults: int) (searchUrl: string) : string =
    searchUrl + $"&expand=changelog&maxResults={maxResults}"

let surround (ch : char) (str : string) = $"{ch}{str}{ch}"
let join (delim : string) (strings : string seq) = String.Join(delim, strings)

type AuthorizedClient(token : string) =
    let client = new HttpClient()
    interface IDisposable with
        member _.Dispose() = client.Dispose()

    member _.GetJSON<'T>(url : string) : Task<Result<'T, string>> =
        task {
            let request = new HttpRequestMessage(HttpMethod.Get, url)
            request.Headers.Add("Authorization", $"Bearer {token}")
            try
                let! response = client.SendAsync(request)
                match response.StatusCode with
                | HttpStatusCode.OK ->
                    let! body = response.Content.ReadAsStringAsync()
                    return Ok(JsonSerializer.Deserialize<'T> body)
                | HttpStatusCode.Unauthorized -> return Error "Bad token"
                | HttpStatusCode.Forbidden -> return Error "Can't access resource"
                | HttpStatusCode.NotFound -> return Error "Not Found"
                | status -> return Error $"Unexpected status: {status}"
            with
                | :? HttpRequestException -> return Error "Could not connect"
                | :? JsonException -> return Error "Bad JSON received"
        }

[<EntryPoint>]
let main (args : string array) =
    match args with
    | [| jiraHostUrl; token; filterId |] ->
        let maxResults = 10_000
        let filterUrl (filterID: string) = $"{jiraHostUrl}/rest/api/2/filter/{filterID}"
        let url = filterUrl(filterId)

        use client = new AuthorizedClient(token)
        client.GetJSON<FilterModel>(url)
        |> Task.bind(Result.bimap Task.FromResult (fun filter ->
            filter.searchUrl |> searchUrl maxResults |> client.GetJSON<SearchModel>)
        |> Task.map(Result.map(fun search ->
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
                let row = ticketNo :: status :: daysInCC :: ticketType :: priority :: component_ :: epicKey :: summary
                          :: date :: flagged :: label :: storyPoints :: createdDate :: statuses
                row |> map (surround '"') |> join "," |> printfn "%s"
        ))
        |> Async.AwaitTask
        |> Async.RunSynchronously
        |> function
            | Ok _ -> 0
            | Error ex ->
                printfn "%s" ex
                1
    | _ ->
        printfn "Bad arguments, use ./JiraExtractor.exe <Jira host URL> <jira token> <filter id>"
        1
