namespace ConfigForge.Abstractions.Annotations;

/// <summary>
/// Groups a property's controls into a named section (a titled box) within its tab.
/// Schema generation emits a JsonForms <c>Group</c> per distinct section, in the order
/// the sections first appear. Properties without a section render directly in the tab.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class CfSectionAttribute : Attribute
{
    /// <summary>Creates the attribute with the section title.</summary>
    /// <param name="section">The section (group box) title.</param>
    public CfSectionAttribute(string section) => Section = section;

    /// <summary>The section title.</summary>
    public string Section { get; }
}
