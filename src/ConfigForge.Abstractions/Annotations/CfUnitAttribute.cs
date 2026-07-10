namespace ConfigForge.Abstractions.Annotations;

/// <summary>
/// Sets the unit suffix (<c>x-cf.controls[path].unit</c>) shown next to a numeric
/// control, for example <c>ms</c> or <c>MB</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class CfUnitAttribute : Attribute
{
    /// <summary>Creates the attribute with the unit suffix.</summary>
    /// <param name="unit">The unit suffix.</param>
    public CfUnitAttribute(string unit) => Unit = unit;

    /// <summary>The unit suffix.</summary>
    public string Unit { get; }
}
