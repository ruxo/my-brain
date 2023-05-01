namespace Tirax.KMS.Pages

open System
open Microsoft.AspNetCore.Components
open FSharp.Data.Adaptive
open Fun.Blazor
open MudBlazor
open Tirax.KMS.Domain

module private Comp =
    type ViewModel =
        { id   :cval<string>
          name :cval<string>
          note :cval<string> }
        
        static member create() = { id = cval(String.Empty); name = cval(String.Empty); note = cval(String.Empty) }

type AddConceptDialog() =
    inherit FunBlazorComponent()
    
    let vm = Comp.ViewModel.create()
        
    [<CascadingParameter>]
    member val MudDialog = Unchecked.defaultof<MudDialogInstance> with get, set
    
    member private my.getResult() =
        let note = vm.note.Value
        { Concept.empty
          with id   = vm.id.Value
               name = vm.name.Value
               note = if String.IsNullOrEmpty(note) then ValueNone else ValueSome note }
    
    member inline private my.mainForm =
        MudForm'() {
            adaptiview(isStatic = true) {
                let! name', setName = vm.name.WithSetter()
                MudTextField'<string>() {
                    Label         "Concept"
                    Required      true
                    RequiredError "Concept name is required"
                    Value         name'
                    ValueChanged  setName
                }
            }
            adaptiview(isStatic = true) {
                let! note, setNote = vm.note.WithSetter()
                MudTextField'<string>() {
                    Label        "Note"
                    Required     false
                    Variant      Variant.Filled
                    Lines        10
                    Value        note
                    ValueChanged setNote
                }
            }
            adaptiview(isStatic = true) {
                let! slug, setSlug = vm.id.WithSetter()
                MudTextField'<string>() {
                    Label        "Slug"
                    Required     false
                    HelperText   "The slug is generated from the name but can be overriden here."
                    Value        slug
                    ValueChanged setSlug
                }
            }
            div {
                classes ["mt-4"; "d-flex"; "justify-space-between"]

                MudButton'() {
                    Variant          Variant.Filled
                    Color            Color.Error
                    DisableElevation true
                    OnClick         (fun _ -> my.MudDialog.Cancel())
                    "Cancel"
                }

                MudButton'() {
                    Variant          Variant.Filled
                    Color            Color.Primary
                    DisableElevation true
                    OnClick         (fun _ -> my.MudDialog.Close(my.getResult()))
                    "Create"
                }
            }
        }
    
    member inline private my.form =
        MudGrid'() {
            Classes ["mb-2"]
            
            MudItem'() {
                xs 12
                sm  7
                MudPaper'() {
                    Classes ["pa-4"]

                    my.mainForm
                }
            }
            MudItem'() {
                xs 12
                sm  5
                MudPaper'() {
                    Classes ["pa-4"; "mud-height-full"]

                    MudText'() { Typo Typo.subtitle2; "0 errors." }
                }
            }
        }

    override my.Render() = 
        MudDialog'() {
            TitleContent (MudText'() { Typo Typo.h3; "Add new concept" })
            DialogContent my.form
        }
        
    static member Show(dialog :IDialogService) =
        async {
            let opts = DialogOptions(FullWidth = true, MaxWidth = MaxWidth.Medium)
            let! dialog = dialog.ShowAsync<AddConceptDialog>("Sample dialog", opts) |> Async.AwaitTask
            let! result = dialog.Result |> Async.AwaitTask
            return if result.Canceled
                   then ValueNone
                   else ValueSome (unbox<Concept> result.Data)
        }