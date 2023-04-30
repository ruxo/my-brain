module Tirax.KMS.AppModel

open FSharp.Data.Adaptive
open Tirax.KMS.Domain

[<Literal>]
let RootTopic = "brain"

[<Literal>]
let MaxHistory = 10

let current_topic = cval(RootTopic)
let history = cval([])

let setMainTopic (topic :ConceptId) =
    Transaction.transact(fun _ ->
        let list = topic::history.Value
        let list = if list.Length > MaxHistory
                   then list |> List.removeAt(list.Length-1)
                   else list
        history.Value <- list
        current_topic.Value <- topic
    )