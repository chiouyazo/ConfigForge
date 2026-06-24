#if !NET8_0_OR_GREATER
namespace System.Runtime.CompilerServices;

#pragma warning disable S2094 // Intentionally empty: a marker type required by the C# compiler.
/// <summary>
/// Compiler shim enabling <c>init</c>-only setters on netstandard2.1, where this
/// type is not part of the framework. Not part of the public ConfigForge surface.
/// </summary>
internal static class IsExternalInit { }
#pragma warning restore S2094
#endif
