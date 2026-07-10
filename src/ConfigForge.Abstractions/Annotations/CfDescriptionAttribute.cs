namespace ConfigForge.Abstractions.Annotations;

/// <summary>
/// Sets the descriptive help text (schema <c>description</c>) shown under a control.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class CfDescriptionAttribute : Attribute
{
    /// <summary>Creates the attribute with the description text.</summary>
    /// <param name="description">The help text shown under the control.</param>
    public CfDescriptionAttribute(string description) => Description = description;

    /// <summary>The description text.</summary>
    public string Description { get; }
}
