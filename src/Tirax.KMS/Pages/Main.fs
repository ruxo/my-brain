namespace Tirax.KMS.Pages

open System.Threading.Tasks
open FSharp.Data.Adaptive
open System.Collections.Generic
open Microsoft.AspNetCore.Components.Web
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
    let MaxTreeDepthRendering = 2
    
    let private emptyItems = HashSet<struct (int*Concept)>()
    
    let inline renderSubTopics (server :Server) (sub_topics :Concept seq) =
        let createModel(level, topics) = HashSet(seq { for t in topics -> struct (level, t) })
        let sub_topics = createModel(0, sub_topics)
        MudTreeView'<struct (int32 * Concept)>() {
            Dense  true
            Hover  true
            Items  sub_topics
            
            SelectedValueChanged(fun struct (_,concept) -> setMainTopic(concept.id))
            
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
                            Items emptyItems
                            Text  concept.name
                            value p
                        }
                )
        }
        
    let inline private renderOwnerBreadcrumbs owners =
        if owners |> Array.isEmpty then
            Internal.emptyNode()
        else
            MudStack'() {
                Row(true); AlignItems(AlignItems.Center); Classes["mb-3"]
                
                MudText'() { Typo Typo.subtitle2; "Owners:" }
                MudBreadcrumbs'() {
                    Items(ConceptBreadcrumbItem.For(owners))
                    Separator("|")
                    ItemTemplate(fun item -> let concept :ConceptBreadcrumbItem = downcast item in showLink concept.Concept)
                    Styles["padding", "0"]
                }
            }

    let private renderConceptTitle(server :Server, topic :Concept, setTopic :Concept -> unit, refreshTopic: unit -> Task<unit>) =
        let saving_status = cval(false)
        let saving_error = cval(ValueNone)
        let topic_editing = cval(false)
        let editing_name = cval(topic.name)
        let topic_saving = cval(false)
                
        let mutable form = Unchecked.defaultof<MudForm>
        let confirmName(key :KeyboardEventArgs) =
            task {
                do! form.Validate()
                if form.IsValid && key.Code.EndsWith("Enter") && not (key.AltKey || key.CtrlKey || key.MetaKey || key.ShiftKey) then
                    if topic.name <> editing_name.Value then
                        topic_saving.Publish(true)
                        do! server.UpdateConceptName(topic.id, editing_name.Value)
                        do! refreshTopic()
                    topic_editing.Publish(false)
            }
            
        html.inject(fun (dialog_service :IDialogService) ->
            adaptiview() {
                let! saving, setSaving = saving_status.WithSetter()
                let! saving_error, setError = saving_error.WithSetter()
                let showDialog() =
                    task {
                        match! AddConceptDialog.Show(dialog_service) with
                        | ValueSome concept -> setSaving(true)
                                               setError(ValueNone)
                                               try
                                                   try
                                                       let! updated_topic = server.addConcept(concept, topic)
                                                       in setTopic(updated_topic)
                                                   with
                                                   | e -> setError(ValueSome e.Message)
                                               finally
                                                   setSaving(false)
                        | ValueNone         -> ()
                    }
                    
                let! owners = server.GetOwner(topic.id).toUICVal()
                in owners |> loadingSection(Seq.toArray >> renderOwnerBreadcrumbs)
                
                let! editing, setEditing = topic_editing.WithSetter()
                let! editing_name, setEditingName = editing_name.WithSetter()
                let! topic_saving = topic_saving
                                
                MudStack'() {
                    Row(true)
                    
                    if editing then
                        MudForm'(){
                            ref(fun v -> form <- v)
                            
                            MudTextField'<string>() {
                                Label("Rename topic")
                                Disabled(topic_saving)
                                Required(true)
                                Value(editing_name)
                                OnKeyUp(confirmName)
                                ValueChanged(setEditingName) 
                            }
                        }
                    else
                        MudText'() { Typo(ConceptTitleTextSize); topic.name }
                        
                    MudIconButton'() { Icon(Icons.Material.Filled.Edit); Disabled(editing); OnClick(fun _ -> setEditing(not editing)) }
                }
                MudStack'() {
                    Row     true
                    Spacing 2
                    
                    MudFab'() {
                        StartIcon(if saving then Icons.Material.Filled.Savings else Icons.Material.Filled.Add)
                        Color(if saving then Color.Dark else Color.Primary)
                        Disabled(saving)
                        OnClick(fun _ -> showDialog())
                    }
                    MudFab'() {
                        StartIcon(Icons.Material.Filled.Bookmark)
                        Label("Bookmark")
                        Disabled(true)
                    }
                    MudFab'() {
                        StartIcon(Icons.Material.Filled.Edit)
                        Color(Color.Secondary)
                        Disabled(true)
                    }
                }
                if saving_error.IsSome then
                    MudAlert'() { Severity Severity.Error; saving_error.Value }
            }
        )
    
    let renderConcept(server :Server, topic :Concept) =
        adaptiview() {
            let! topic, setTopic = cval(topic).WithSetter()
            let refreshTopic() =
                task {
                    let! refreshed_topic = server.fetch(topic.id)
                    setTopic(refreshed_topic.Value)
                }
            
            renderConceptTitle(server, topic, setTopic, refreshTopic)
        
            MudDivider'.create()
            
            if not topic.contains.IsEmpty then
                MudText'() { Typo ConceptDetailTitleTextSize; "Contains" }
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
            
            MudPaper'() {
                Classes   ["pa-3"; "ma-2"]
                Height    ("50em")
                Width     ("50em")
                Elevation (2)
                Outlined  (true)
                MudText'() { Typo ConceptDetailTitleTextSize; "Note" }
                MudText'() { topic.note.defaultValue(System.String.Empty) }
            }
        }

type Pages with
    static member Main(server :Server) =
        adaptiview() {
            let! topic = current_topic
            let! result = server.fetch(topic).toUICVal()
            
            result |> loadingSection (fun concept ->
                match concept with
                | ValueSome c -> MainPage.renderConcept(server, c)
                | ValueNone -> MudAlert'() {
                                   Severity Severity.Error
                                   $"Cannot load topic: {topic}. Please try again."
                               }
            )
        }