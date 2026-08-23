namespace ConfigForge.Abstractions.Annotations;

/// <summary>
/// Marks a property as never rendered in the form (schema <c>x-hidden</c>). The value still lives
/// in the document and is readable by actions and by rules that watch it, so it can hold pure UI
/// state — e.g. a "connection tested" flag that gates a tab — without appearing as an input.
/// Unlike <c>[CfUntracked]</c> (which hides nothing, only excludes from persistence) this removes
/// the control entirely; combine the two for a transient, invisible status field.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class CfHiddenAttribute : Attribute { }
