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
                    title { "Fun Blazor" }
                    baseUrl "/"
                    meta { charset "utf-8" }
                    meta {
                        name "viewport"
                        content "width=device-width, initial-scale=1.0"
                    }
                }
                body {
                    rootComp<Index> ctx RenderMode.ServerPrerendered
                    script { src "_framework/blazor.server.js" }
#if DEBUG
                    html.hotReloadJSInterop
#endif
                }
            }
        }