namespace ConfigForge.Abstractions.Annotations;

/// <summary>
/// Overrides the control type inferred for a property when a schema is generated
/// from a CLR type. The value is a ConfigForge control type such as
/// <c>textarea</c>, <c>slider</c>, <c>code</c>, or <c>tags</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class CfControlAttribute : Attribute
{
    /// <summary>Creates the attribute with the control type to force.</summary>
    /// <param name="controlType">The ConfigForge control type, for example <c>textarea</c>.</param>
    public CfControlAttribute(string controlType) => ControlType = controlType;

    /// <summary>The ConfigForge control type to render this property with.</summary>
    public string ControlType { get; }
}
