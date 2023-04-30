namespace Tirax.KMS

open Microsoft.AspNetCore.Mvc.Rendering
open Fun.Blazor

type Index() =
    inherit FunBlazorComponent()

    override _.Render() =
#if DEBUG       
        html.hotReloadComp (app, "Tirax.KMS.App.app")
#else
        Tirax.KMS.App.app
#endif

    static member page ctx =
        fragment {
            doctype "html"
            html' {
                head {
                    title { "Tirax KMS" }
                    baseUrl "/"
                    meta { charset "utf-8" }
                    meta {
                        name "viewport"
                        content "width=device-width, initial-scale=1.0"
                    }
                    stylesheet "https://fonts.googleapis.com/css?family=Roboto:300,400,500,700&display=swap"
                    stylesheet "_content/MudBlazor/MudBlazor.min.css"
                }
                body {
                    rootComp<Index> ctx RenderMode.ServerPrerendered
                    
                    script { src "_content/MudBlazor/MudBlazor.min.js" }
                    script { src "_framework/blazor.server.js" }
#if DEBUG
                    html.hotReloadJSInterop
#endif
                }
            }
        }