using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Extensions;

namespace Tirax.KMS.App.Features.Home;

public partial class AddDialog : ComponentBase
{
    string conceptName = string.Empty;
    string[] errors = System.Array.Empty<string>();
    MudForm form = default!;

    [CascadingParameter]
    public MudDialogInstance MudDialog { get; set; } = default!;

    bool HasErrors => errors.Length > 0;
    
    public static async Task<Option<string>> Show(IDialogService dialog) {
        var opts = new DialogOptions{ FullWidth = true, MaxWidth = MaxWidth.Medium };
        var d = await dialog.ShowAsync<AddDialog>("Sample dialog", opts);
        var result = await d.Result;
        return result.Canceled? None : result.Data.As<Option<string>>();
    }

    void CancelDialog() {
        MudDialog.Cancel();
    }

    async Task ConfirmSave() {
        await form.Validate();
        if (!form.Errors.Any())
            MudDialog.Close(Some(conceptName));
    }
}