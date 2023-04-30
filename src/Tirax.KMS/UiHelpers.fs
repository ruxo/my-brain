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
| LoadError e -> p { e.ToString() }
| Data data -> renderer data

let showLink (concept :Domain.Concept) =
    MudLink'() {
        Color Color.Info
        
        onclick(fun _ -> AppModel.setMainTopic concept.id)
        
        concept.name
    }