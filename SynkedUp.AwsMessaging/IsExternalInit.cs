#if !NET5_0_OR_GREATER
// Polyfill so `record` / `init`-only setters compile on netstandard2.0.
// The compiler emits references to this exact type for init accessors; it is
// absent from netstandard2.0's BCL, so it MUST live in this namespace — it
// cannot be moved under SynkedUp.AwsMessaging. Guarded so it vanishes on net5.0+.
// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
#endif
