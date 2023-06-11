using Microsoft.AspNetCore.Components;
using MudBlazor;
using Tirax.KMS.App.Extensions;

namespace Tirax.KMS.App.Features.Topic;

public partial class AddDialog : ComponentBase, IDialog<string>
{
    string conceptName = string.Empty;
    string[] errors = System.Array.Empty<string>();
    MudForm form = default!;

    [CascadingParameter]
    public MudDialogInstance MudDialog { get; set; } = default!;

    bool HasErrors => errors.Length > 0;
    
    void CancelDialog() {
        MudDialog.Cancel();
    }

    async Task ConfirmSave() {
        await form.Validate();
        if (!form.Errors.Any())
            MudDialog.Close(conceptName);
    }
}