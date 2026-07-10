namespace ConfigForge.Abstractions.Annotations;

/// <summary>
/// Marks a property as untracked: edits to it never count towards the unsaved
/// changes state. Schema generation emits <c>x-cf.controls[path].tracked = false</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class CfUntrackedAttribute : Attribute { }
