namespace ConfigForge.Abstractions.Annotations;

/// <summary>
/// Sets the tooltip (<c>x-cf.controls[path].tooltip</c>) shown when hovering a
/// control's label.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class CfTooltipAttribute : Attribute
{
    /// <summary>Creates the attribute with the tooltip text.</summary>
    /// <param name="tooltip">The tooltip text.</param>
    public CfTooltipAttribute(string tooltip) => Tooltip = tooltip;

    /// <summary>The tooltip text.</summary>
    public string Tooltip { get; }
}
