using Microsoft.JSInterop;

namespace Tirax.KMS.App.Services.Interop;

public sealed class KmsJs
{
    readonly IJSRuntime js;
    public KmsJs(IJSRuntime js) {
        this.js = js;
    }

    /// <summary>
    /// Prevent the reconnection message display from the Blazor framework, and break its connection. This should be used only when leaving from
    /// a Blazor page since it breaks the reconnection logic code. The feature will be unusable until a Blazor page is reloaded.
    /// </summary>
    public ValueTask BreakReconnection() => js.InvokeVoidAsync("kms.breakReconnection");
}