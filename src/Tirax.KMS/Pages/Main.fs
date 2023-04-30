namespace Tirax.KMS.Pages

open FSharp.Data.Adaptive
open System.Collections.Generic
open RZ.FSharp.Extension
open MudBlazor
open Fun.Blazor
open Tirax.KMS
open Tirax.KMS.Server
open Tirax.KMS.Domain
open Tirax.KMS.AppModel

type Pages = struct end

module private MainPage =
    [<Literal>]
    let MaxTreeDepthRendering = 3
    
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
                    let should_expand = level < 1
                    if level < MaxTreeDepthRendering then
                        adaptiview(isStatic=true){
                            let! sub_sub_topics = server.fetch(concept.contains).toCVal(Seq.empty)
                            let sub_sub_items = createModel(level+1, sub_sub_topics)
                            MudTreeViewItem'() {
                                Items    sub_sub_items
                                Expanded should_expand
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
    
    let renderConcept (server :Server) (topic :Concept) =
        let show_dialog = cval(false)
        html.inject(fun (dialog_service :IDialogService) ->
            let opts = DialogOptions(CloseOnEscapeKey = true)

            let showDialog() =
                dialog_service.Show<AddConceptDialog>("Sample dialog") |> ignore
            
            fragment {
                adaptiview() {
                    let! show, setShow = show_dialog.WithSetter()
                    
                    MudStack'() {
                        Row     true
                        Spacing 2
                        
                        MudText'() { Typo ConceptTitleTextSize; topic.name }
                        MudFab'() { StartIcon Icons.Material.Filled.Add     ; Color Color.Primary  ; OnClick(fun _ -> showDialog()) }
                        MudFab'() { StartIcon Icons.Material.Filled.Bookmark; Label "Bookmark"      }
                        MudFab'() { StartIcon Icons.Material.Filled.Edit    ; Color Color.Secondary }
                    }
                }
                
                MudDivider'.create()
                
                if not topic.contains.IsEmpty then
                    adaptiview() {
                        let! sub_topics_result = server.fetch(topic.contains).toUICVal()
                        sub_topics_result |> loadingSection(renderSubTopics server)
                    }
                
                if topic.link.IsSome then
                    MudPaper'() {
                        MudText'() { Typo ConceptDetailTitleTextSize; "References" }
                        
                        let link = topic.link.Value.ToString()
                        MudLink'() {
                            Href   link
                            Target "_blank"
                            
                            link
                        }
                    }
                
                if topic.note.IsSome then
                    MudPaper'() {
                        MudText'() { Typo ConceptDetailTitleTextSize; "Note" }
                        MudText'() { topic.note.Value }
                    }
            }
        )

type Pages with
    static member Main(server :Server) =
        adaptiview() {
            let! topic = current_topic
            let! result = server.fetch(topic).toUICVal()
            
            result |> loadingSection (fun concept ->
                match concept with
                | ValueSome c -> MainPage.renderConcept server c
                | ValueNone -> MudAlert'() {
                                   Severity Severity.Error
                                   
                                   $"Cannot load topic: {topic}. Please try again."
                               }
            )
        }