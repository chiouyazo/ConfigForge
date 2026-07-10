namespace ConfigForge.Abstractions.Annotations;

/// <summary>
/// Marks a property as a write-only secret so schema generation emits the
/// <c>secret</c> control type. See <see cref="ConfigForgeSecret"/> for the
/// stored/change/clear semantics a host applies on load and save.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class CfSecretAttribute : Attribute { }
