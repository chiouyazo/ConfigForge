namespace ConfigForge.Abstractions.Annotations;

/// <summary>
/// Excludes a property from a generated schema entirely, so it is neither
/// rendered nor written back by the editor.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class CfIgnoreAttribute : Attribute { }
