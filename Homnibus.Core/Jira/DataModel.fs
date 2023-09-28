[<AutoOpen>]
module Homnibus.Core.Jira.DataModel
open System.Text.Json.Serialization

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
    [<JsonPropertyName("toString")>]
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
