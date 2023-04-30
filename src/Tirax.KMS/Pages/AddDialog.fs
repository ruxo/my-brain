namespace Tirax.KMS.Pages

open Fun.Blazor
open Microsoft.AspNetCore.Components
open MudBlazor

type AddConceptDialog() =
    inherit FunBlazorComponent()
    
    [<CascadingParameter>]
    member val MudDialog = Unchecked.defaultof<MudDialogInstance> with get, set
    
    override this.Render() = 
        MudDialog'() {
            DialogContent "Hello!"
        }