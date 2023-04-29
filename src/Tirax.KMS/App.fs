// hot-reload
// hot-reload is the flag to let cli know this file should be included
// It has dependency requirement: the root is the app which is used in the Index.fs
// All other files which want have hot reload, need to drill down to that file, and all the middle file should also add the '// hot-reload' flag at the top of that file
[<AutoOpen>]
module Tirax.KMS.App

open FSharp.Data.Adaptive
open Fun.Blazor
open RZ.FSharp.Extension
open Domain
open Server

[<Literal>]
let RootTopic = "brain"

[<Literal>]
let MaxHistory = 10

let current_topic = cval(RootTopic)
let history = cval(System.Collections.Generic.List<ConceptId>(MaxHistory + 1))

let setMainTopic topic =
    Transaction.transact(fun _ ->
        let list = history.Value
        list.Add(topic)
        if list.Count > MaxHistory then list.RemoveAt(0)
        history.Value <- list
        current_topic.Value <- topic
    )
    
let loadingSection renderer = function
| Loading -> html.raw "💿"
| LoadError e -> p { e.ToString() }
| Data data -> renderer data

let showLink concept =
    a {
        onclick(fun _ -> setMainTopic concept.id)
        concept.name
    }
    
let renderLinks (server :Server) (topic :Concept) =
    fragment {
        h1 { topic.name }
        
        if topic.link.IsSome then
            h2 { "References" }
            
            a { topic.link.Value.ToString() }
        
        adaptiview() {
            let! sub_topics_result = server.fetch(topic.contains).toUICVal()
            sub_topics_result |> loadingSection (fun sub_topics ->
                ul {
                    childContent (sub_topics.map(fun concept ->
                        li {
                            showLink concept
                            br
                            if not concept.contains.IsEmpty then
                                adaptiview() {
                                    let! sub_topics3 = server.fetch(concept.contains).toCVal(Seq.empty)
                                    ul {
                                        childContent [for topic3 in sub_topics3 -> li { showLink topic3 }]
                                    }
                                }
                        }
                    ))
                }
            )
        }
        
        if topic.note.IsSome then
            h2 { "Note" }
            p { topic.note.Value }
    }
    
let app = html.inject(fun (server :Server) ->
    adaptiview() {
        let! topic = current_topic
        let! history = server.fetch(history.Value).toUICVal()
        div {
            childContent(seq {
                button {
                    onclick (fun _ -> setMainTopic RootTopic)
                    "Home"
                }
                html.raw "&nbsp;"
                history |> loadingSection(fun list ->
                    list.map(showLink).join(html.raw " ⪧ ") |> html.mergeNodes
                )
            })
        }
        
        let! result = server.fetch(topic).toUICVal()
        result |> loadingSection (fun concept ->
            match concept with
            | ValueSome c -> renderLinks server c
            | ValueNone -> h1 { $"Invalid topic: {topic}" }
        )
    }
)