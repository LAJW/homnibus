[<AutoOpen>]
module Homnibus.Core.DomainModel
open System

type Status = {
    Name : string
    Date : DateTime
}

type Ticket = {
    TicketNo : string
    Status : string option
    DaysInColumn : int
    TicketType : string option
    Priority : string option
    Components : string list
    Summary : string option
    Flagged : bool
    Labels : string list
    StoryPoints : int option
    CreatedDate : DateTime
    Statuses : Status list
}
