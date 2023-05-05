[<AutoOpen>]
module Tirax.KMS.UiHelpers

open System.Runtime.CompilerServices
open System.Threading.Tasks
open FSharp.Data.Adaptive
open Fun.Blazor
open Fun.Result
open MudBlazor

[<Struct; IsReadOnly>]
type UiAsyncResult<'T> =
    | Loading
    | LoadError of error:exn
    | Data of 'T
    
let uiAsync x = async {
    try
        let! data = x
        return (Data data)
    with
    | e -> return (LoadError e)
}

let ConceptTitleTextSize       = Typo.h2
let ConceptDetailTitleTextSize = Typo.h4

[<RequireQualifiedAccess>]
module AVal =
    let inline ofAsync def (v :Async<'a>) = v |> Async.StartAsTask |> AVal.ofTask def :?> cval<'a>
    
type Async<'T> with
    member inline my.toCVal(def) = my |> AVal.ofAsync def
    member inline my.forUI() = my |> uiAsync
    member        my.toUICVal() = my |> uiAsync |> AVal.ofAsync Loading
    
type ChangeableValue<'T> with
    member inline my.schedule (task :Task<'T>) = task |> Task.map my.Publish |> ignore; my
    
let loadingSection renderer = function
| Loading -> html.raw "💿"
| LoadError e -> MudAlert'() {
                     Severity  Severity.Warning
                     
                     div {
                         div { "Loading failed. Please try again later." }
                         MudDivider'.create()
                         div { e.ToString() }
                     }
                 }
| Data data -> renderer data

let showLink (concept :Domain.Concept) =
    MudLink'() {
        Color Color.Info
        
        onclick(fun _ -> AppModel.setMainTopic concept.id)
        
        concept.name
    }
    
// ====================================== LINK FOR BREADCRUMBS ========================================
open System.Linq
open RZ.FSharp.Extension
[<Sealed>]
type ConceptBreadcrumbItem(concept :Domain.Concept) =
    inherit BreadcrumbItem(concept.name, "#")
    
    static let toItem concept = ConceptBreadcrumbItem(concept) :> BreadcrumbItem
    
    member _.Concept = concept
    
    static member For(concepts :Domain.Concept seq) = concepts.map(toItem).ToList()