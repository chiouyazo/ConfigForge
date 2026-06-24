#if !NET8_0_OR_GREATER
namespace System.Runtime.CompilerServices;

/// <summary>
/// Compiler shim that enables <c>init</c> accessors (and record positional
/// setters) on targets, such as netstandard2.1, that do not ship this type.
/// </summary>
internal static class IsExternalInit { }
#endif
