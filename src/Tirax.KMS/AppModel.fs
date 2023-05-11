module Tirax.KMS.AppModel

open FSharp.Data.Adaptive
open Tirax.KMS.Domain

type NativeList<'T> = System.Collections.Generic.List<'T>

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
    
type Versioning<'T> = (struct (uint32 * 'T))
    
type ViewModel() =
    let current_topic = cval(RootTopic)
    let history = cval(struct (0u, NativeList<ConceptId>()))
    
    member _.CurrentTopic = current_topic
    member _.History = history
    
    member my.SetMainTopic(topic :ConceptId) =
        Transaction.transact(fun _ ->
            let struct (v,list) = history.Value
            list.Add(topic)
            if list.Count > MaxHistory then list.RemoveAt(0)
            history.Value <- (v+1u, list)
            current_topic.Value <- topic
        )