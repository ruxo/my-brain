// hot-reload
// hot-reload is the flag to let cli know this file should be included
// It has dependency requirement: the root is the app which is used in the Index.fs
// All other files which want have hot reload, need to drill down to that file, and all the middle file should also add the '// hot-reload' flag at the top of that file
[<AutoOpen>]
module Tirax.KMS.App

open System.Linq
open Microsoft.AspNetCore.Components.Routing
open FSharp.Data.Adaptive
open RZ.FSharp.Extension
open Fun.Blazor
open Fun.Blazor.Operators
open MudBlazor
open Tirax.KMS.Pages
open Server
open AppModel
open Domain
    
type MudNavLink with
    static member inline of'(path, title :string, ?match' :NavLinkMatch) =
        MudNavLink'(){
            Href path
            if match'.IsSome then "Match" => match'.Value else Internal.emptyAttr()
            title
        }
    
let private navigation =
    MudNavMenu'() {
        MudNavLink.of'("/", "Home", NavLinkMatch.All)
        MudNavGroup'() {
            Title "Favorites"
        }
    }
    
let private drawer_open = cval(false)

let inline private topBar (server :Server) =
    MudAppBar'() {
        Elevation 1
        
        adaptiview() {
            let! drawer_open, setOpen = drawer_open.WithSetter()
            
            MudIconButton'() {
                Icon    Icons.Material.Filled.Menu
                Color   Color.Inherit
                Edge    Edge.Start
                OnClick(fun _ -> setOpen(not drawer_open))
            }
        }
        
        MudLink'() {
            Typo      Typo.h5
            Color     Color.Surface
            Underline Underline.None
            Classes   ["mx-3"]
            
            OnClick(fun _ -> setMainTopic(RootTopic))
            
            "Tirax KMS"
        }
    }
    
[<Sealed>]
type private ConceptBreadcrumbItem(concept :Domain.Concept) =
    inherit BreadcrumbItem(concept.name, "#")
    
    member _.Concept = concept

let private breadcrumbs(server :Server) =
    adaptiview() {
        let! history = history
        let! history = server.fetch(history.rev()).toUICVal()
        history |> loadingSection(fun list -> let items = seq { for h in list -> ConceptBreadcrumbItem(h) :> BreadcrumbItem }
                                              MudBreadcrumbs'() {
                                                  Separator("⪧")
                                                  Items(items.ToList())
                                                  ItemTemplate(fun item -> let concept :ConceptBreadcrumbItem = downcast item in showLink concept.Concept)
                                              })
    }
    
let private drawer =
    adaptiview() {
        let! is_open = drawer_open
        MudDrawer'() {
            ClipMode  DrawerClipMode.Always
            Elevation 2
            Open      is_open
            navigation
        }
    }
    
let app = html.inject(fun (server :Server) ->
    fragment {
        // Providers need to be with app in order to create/destroy with app rendering
        MudThemeProvider'.create()
        MudDialogProvider'.create()
        MudSnackbarProvider'.create()
                    
        MudLayout'() {
            topBar(server)
            drawer
            
            MudMainContent'() {
                breadcrumbs(server)
                div {
                    classes  ["ma-3"; "pa-3"]
                    
                    Pages.Main(server)
                }
            }
        }
    }
)