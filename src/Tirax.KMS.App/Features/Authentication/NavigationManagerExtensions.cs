using Microsoft.AspNetCore.Components;

namespace Tirax.KMS.App.Features.Authentication;

public static class NavigationManagerExtensions
{
    public static void NavigateToLogin(this NavigationManager navManager) {
        var loginUri = $"/auth/login?redirectUri={Uri.EscapeDataString(navManager.Uri)}";
        navManager.NavigateTo(loginUri, forceLoad: true);
    }

    public static void NavigateToLogout(this NavigationManager navManager) {
        navManager.NavigateTo("/auth/logout", forceLoad: true);
    }
}