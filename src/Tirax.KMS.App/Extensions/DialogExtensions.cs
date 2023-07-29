using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Extensions;

namespace Tirax.KMS.App.Extensions;

// ReSharper disable once UnusedTypeParameter
public interface IDialog<T> { }

public static class DialogExtensions
{
    public static async Task<Option<R>> ShowDialog<T,R>(this IDialogService dialog, string title, DialogOptions? options = null)
        where T : ComponentBase, IDialog<R> {
        var opts = options ?? new DialogOptions{ FullWidth = true, MaxWidth = MaxWidth.Medium };
        var d = await dialog.ShowAsync<T>(title, opts);
        var result = await d.Result;
        return (result.Canceled? None : Some(result.Data.As<R>()))!;
    }
    
}