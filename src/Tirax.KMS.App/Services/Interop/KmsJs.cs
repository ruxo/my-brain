using Microsoft.JSInterop;

namespace Tirax.KMS.App.Services.Interop;

public sealed class KmsJs
{
    readonly IJSRuntime js;
    public KmsJs(IJSRuntime js) {
        this.js = js;
    }

    /// <summary>
    /// Properly redirect to an external website (or any non-Blazor page in the running application, such as a Razor page).
    /// </summary>
    /// <param name="externalUrl">The URL of the destination</param>
    public async ValueTask RedirectTo(string externalUrl) {
        await js.InvokeVoidAsync("kms.breakReconnection");
        await js.InvokeVoidAsync("kms.redirectTo", externalUrl);
    }
    
    public ValueTask NavigateToLogin(string currentUrl) => 
        RedirectTo($"/auth/login?redirectUri={Uri.EscapeDataString(currentUrl)}");

    public ValueTask NavigateToLogout() => 
        RedirectTo("/auth/logout");
}