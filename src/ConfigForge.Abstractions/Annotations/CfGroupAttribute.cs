namespace ConfigForge.Abstractions.Annotations;

/// <summary>
/// Assigns a top-level property to a named sidebar group. When any property declares a
/// group, schema generation builds a two-level navigation: groups form the sidebar and
/// each group's <see cref="CfCategoryAttribute"/> values become sub-tabs within it.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class CfGroupAttribute : Attribute
{
    /// <summary>Creates the attribute with the group name.</summary>
    /// <param name="group">The sidebar group this property belongs to.</param>
    public CfGroupAttribute(string group) => Group = group;

    /// <summary>The sidebar group name.</summary>
    public string Group { get; }
}
