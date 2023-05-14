using LanguageExt.UnsafeValueAccess;

namespace Tirax.KMS.Extensions;

public static class OptionExtension
{
    public static bool IfSome<T>(this Option<T> value, out T some) {
        some = value.ValueUnsafe();
        return value.IsSome;
    }
}