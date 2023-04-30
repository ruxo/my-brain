namespace Tirax.KMS

open System.Collections.Generic
open RZ.FSharp.Extension
open MudBlazor
open Fun.Blazor
open Server
open Domain
open AppModel

type Pages = struct end

module private MainPage =
    let inline renderSubTopics (server :Server) (sub_topics :Concept seq) =
        let createModel(level, topics) = HashSet(seq { for t in topics -> struct (level, t) })
        let sub_topics = createModel(0, sub_topics)
        MudTreeView'<struct (int32 * Concept)>() {
            Dense  true
            Hover  true
            Items  sub_topics
            
            SelectedValueChanged(fun concept -> setMainTopic(concept.snd().id))
            
            ItemTemplate(
                fun (struct (level, concept) as p) ->
                    if level < 3 then
                        adaptiview(isStatic=true){
                            let! sub_sub_topics = server.fetch(concept.contains).toCVal(Seq.empty)
                            let sub_sub_items = createModel(level+1, sub_sub_topics)
                            MudTreeViewItem'() {
                                Items    sub_sub_items
                                Expanded true
                                Text     concept.name
                                value    p
                            }
                        }
                    else
                        MudTreeViewItem'() {
                            Text  concept.name
                            value p
                        }
                )
        }
    
    let renderLinks (server :Server) (topic :Concept) =
        fragment {
            MudText'() { Typo Typo.h3; topic.name }
            MudDivider'.create()
            
            if not topic.contains.IsEmpty then
                adaptiview() {
                    let! sub_topics_result = server.fetch(topic.contains).toUICVal()
                    sub_topics_result |> loadingSection(renderSubTopics server)
                }
            
            if topic.link.IsSome then
                MudPaper'() {
                    MudText'() { Typo Typo.h4; "References" }
                    
                    let link = topic.link.Value.ToString()
                    MudLink'() { Href link; link }
                }
            
            if topic.note.IsSome then
                MudPaper'() {
                    MudText'() { Typo Typo.h4; "Note" }
                    MudText'() { topic.note.Value }
                }
        }

type Pages with
    static member Main(server :Server) =
        adaptiview() {
            let! topic = current_topic
            let! result = server.fetch(topic).toUICVal()
            
            result |> loadingSection (fun concept ->
                match concept with
                | ValueSome c -> MainPage.renderLinks server c
                | ValueNone -> MudAlert'() {
                                   Severity Severity.Error
                                   
                                   $"Cannot load topic: {topic}. Please try again."
                               }
            )
        }