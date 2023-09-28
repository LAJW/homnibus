[<AutoOpen>]
module Homnibus.Core.Jira.Prelude
open System

// "2022-03-03T08:38:02.000+0000"
let dateFormat = "yyyy-MM-dd'T'HH:mm:ss.SSSZ"
let parseDate (str : string) : DateTime = DateTime.Parse(str) // TODO: Try using exact
let dateOutFormat = "dd-MM-yyyy"

let inline name(self : ^T when ^T : (member name : ^V)) = (^T : (member name : ^V) self)

module DateTime =
    let toString (format : string) (date : DateTime) = date.ToString(format)

type Int32 with
    static member tryParse(x : string) : int option =
        match Int32.TryParse x with
        | true, value -> Some value
        | false, _ -> None

// C#-style None-coalescing operator
let inline (|??) (opt : 'T option) (defaultValue : 'T) =
    opt |> Option.defaultValue defaultValue
