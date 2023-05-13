namespace Tirax.KMS.Pages

// module private Comp =
//     type ViewModel =
//         { id   :cval<string>
//           name :cval<string>
//           note :cval<string> }
//         
//         static member create() = { id = cval(String.Empty); name = cval(String.Empty); note = cval(String.Empty) }
//         
//     let private special_characters = Regex(@"[^a-z0-9\s-.]", RegexOptions.Compiled)
//     let private spaces             = Regex(@"\s+"          , RegexOptions.Compiled)
//     let private valid_slug         = Regex(@"^[a-z0-9-.]+$", RegexOptions.Compiled)
//     
//     let inline private replace (regex :Regex) (value :string) s = regex.Replace(s, value)
//         
//     let generateSlug(s :string) =
//         s.ToLowerInvariant()
//         |> replace special_characters String.Empty
//         |> replace spaces "-"
//         
//     let isSlugValid (s :string) =
//         valid_slug.IsMatch(s)
//
// type AddConceptDialog() =
//     inherit FunBlazorComponent()
//     
//     let vm = Comp.ViewModel.create()
//     
//     let slug_overriden = cval(ValueNone)
//     let slug_from_name = cval(String.Empty)
//         
//     [<CascadingParameter>]
//     member val MudDialog = Unchecked.defaultof<MudDialogInstance> with get, set
//     
//     member private my.getResult() =
//         let note = vm.note.Value
//         { Concept.empty
//           with id   = vm.id.Value
//                name = vm.name.Value
//                note = if String.IsNullOrEmpty(note) then ValueNone else ValueSome note }
//     
//     member inline private my.mainForm (errors :cval<string array>) =
//         let mutable form = Unchecked.defaultof<MudForm>
//         
//         let save _ =
//             task {
//                 do! form.Validate()
//                 let err = form.Errors
//                 if err.isEmpty() then
//                     let final_slug = slug_overriden.Value.defaultValue(slug_from_name.Value)
//                     transact(fun() -> vm.id.Value <- final_slug)
//                     my.MudDialog.Close(my.getResult())
//                 else
//                     transact(fun() -> errors.Value <- err)
//             }
//             
//         adaptiview() {
//             let! name', setName = vm.name.WithSetter()
//                 
//             let nameChanged s =
//                 task {
//                     slug_from_name.Publish(Comp.generateSlug s)
//                     setName(s)
//                     form.ResetValidation()
//                 }
//             
//             MudForm'() {
//                 ErrorsChanged (fun e -> errors.Publish(e))
//                 ref           (fun v -> form <- v)
//                 
//                 MudTextField'<string>() {
//                     Label         "Concept"
//                     Required      true
//                     RequiredError "Concept name is required"
//                     Value         name'
//                     ValueChanged  nameChanged
//                 }
//                 
//                 adaptiview(isStatic = true) {
//                     let! note, setNote = vm.note.WithSetter()
//                     MudTextField'<string>() { Label("Note"); Required(false); Variant(Variant.Filled); Lines(10); Value(note); ValueChanged(setNote) }
//                 }
//                 adaptiview(isStatic = true) {
//                     let! overriden, setOverriden = slug_overriden.WithSetter()
//                     let! slug = slug_from_name
//                     let display_slug = overriden.defaultValue(slug)
//                     MudTextField'<string>() {
//                         Label        "Slug"
//                         Required     true
//                         HelperText   "The slug is generated from the name but can be overriden here."
//                         Value        display_slug
//                         ValueChanged (ValueSome >> setOverriden)
//                         Validation   (Func<string,bool> Comp.isSlugValid)
//                     }
//                 }
//             }
//             div {
//                 classes ["mt-4"; "d-flex"; "justify-space-between"]
//
//                 MudButton'() {
//                     Color   Color.Error
//                     OnClick (ignore >> my.MudDialog.Cancel)
//                     "Cancel"
//                 }
//
//                 adaptiview(isStatic = true) {
//                     let! errs = errors
//                     
//                     MudButton'() {
//                         Variant  Variant.Filled
//                         Color    Color.Primary
//                         Disabled (errs.Length > 0)
//                         OnClick  save
//                         "Create"
//                     }
//                 }
//             }
//         }
//     
//     member inline private my.form =
//         let errors = cval([||])
//         
//         MudGrid'() {
//             Classes ["mb-2"]
//             
//             MudItem'() {
//                 xs 12
//                 sm  7
//                 MudPaper'() {
//                     Classes ["pa-4"]
//                     my.mainForm errors
//                 }
//             }
//             MudItem'() {
//                 xs 12
//                 sm  5
//                 MudPaper'() {
//                     Classes ["pa-4"; "mud-height-full"]
//                     adaptiview(){
//                         let! errs = errors
//                             
//                         MudText'() {
//                             Typo Typo.subtitle2
//                             $"{errs.Length} error(s)."
//                         }
//                         html.mergeNodes(seq { for e in errs -> MudText'() { Color Color.Error; e } })
//                     }
//                 }
//             }
//         }
//
//     override my.Render() = 
//         MudDialog'() {
//             TitleContent (MudText'() { Typo Typo.h3; "Add new concept" })
//             DialogContent my.form
//         }
//         
//     static member Show(dialog :IDialogService) =
//         async {
//             let opts = DialogOptions(FullWidth = true, MaxWidth = MaxWidth.Medium)
//             let! dialog = dialog.ShowAsync<AddConceptDialog>("Sample dialog", opts) |> Async.AwaitTask
//             let! result = dialog.Result |> Async.AwaitTask
//             return if result.Canceled
//                    then ValueNone
//                    else ValueSome (unbox<Concept> result.Data)
//         }