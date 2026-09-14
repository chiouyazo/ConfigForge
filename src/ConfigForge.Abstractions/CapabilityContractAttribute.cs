namespace ConfigForge.Abstractions;

/// <summary>
/// Marks an interface as a capability contract: safe and intended to be served remotely by a
/// <c>CapabilityLoader</c> proxy instead of requiring the caller to load the real implementation.
/// </summary>
/// <remarks>
/// Only apply this to an interface a widget depends on purely for its own data (e.g. a log
/// store, a connector status query) where the real implementation touches product-specific
/// state the caller must never instantiate. Never apply it to an ordinary framework or hosting
/// dependency (<c>NavigationManager</c>, <c>IJSRuntime</c>, DI-supplied services), since those
/// are supplied locally by whichever process actually renders the widget, not proxied over HTTP.
/// </remarks>
[AttributeUsage(AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
public sealed class CapabilityContractAttribute : Attribute { }
