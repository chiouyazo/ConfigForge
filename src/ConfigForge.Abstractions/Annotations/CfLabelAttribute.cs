namespace ConfigForge.Abstractions.Annotations;

/// <summary>
/// Sets the human readable label (schema <c>title</c>) for a property. Without it,
/// schema generation derives a title from the property name.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class CfLabelAttribute : Attribute
{
    /// <summary>Creates the attribute with the label text.</summary>
    /// <param name="label">The label shown above the control.</param>
    public CfLabelAttribute(string label) => Label = label;

    /// <summary>The label text.</summary>
    public string Label { get; }
}
