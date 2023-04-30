namespace Tirax.KMS

open RZ.FSharp.Extension
open Fun.Blazor
open Server
open Domain
open AppModel

type Pages = struct end

module private MainPage =
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

type Pages with
    static member Main(server :Server) =
        adaptiview() {
            let! topic = current_topic
            let! result = server.fetch(topic).toUICVal()
            
            result |> loadingSection (fun concept ->
                match concept with
                | ValueSome c -> MainPage.renderLinks server c
                | ValueNone -> h1 { $"Invalid topic: {topic}" }
            )
        }