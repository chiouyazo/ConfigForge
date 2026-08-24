namespace ConfigForge.Abstractions.Annotations;

/// <summary>
/// Base for the section-level conditional-rule attributes
/// (<see cref="CfSectionEnableWhenAttribute"/>, <see cref="CfSectionVisibleWhenAttribute"/>).
/// Placed on the config <b>type</b> and keyed by a section name (a <c>[CfSection]</c> value).
/// Gates a whole section — most visibly the sub-tab a <c>oneof</c> variant renders for that
/// section — against another field's value (e.g. lock an entry's "Config" sub-tab until its
/// connection has been tested).
/// </summary>
public abstract class CfSectionRuleAttribute : Attribute
{
    /// <summary>Creates a section rule that watches a field for equality.</summary>
    /// <param name="section">The section name (matches a <c>[CfSection]</c>).</param>
    /// <param name="fieldPath">Slash path to the watched field, relative to the section's container.</param>
    /// <param name="equalsValue">The value the watched field must equal.</param>
    protected CfSectionRuleAttribute(string section, string fieldPath, object? equalsValue)
    {
        Section = section;
        FieldPath = fieldPath;
        EqualsValue = equalsValue;
        Condition = CfCondition.Equals;
    }

    /// <summary>Creates a section rule that watches a field for presence.</summary>
    /// <param name="section">The section name.</param>
    /// <param name="fieldPath">Slash path to the watched field.</param>
    /// <param name="condition">The presence comparison to apply.</param>
    protected CfSectionRuleAttribute(string section, string fieldPath, CfCondition condition)
    {
        Section = section;
        FieldPath = fieldPath;
        EqualsValue = null;
        Condition = condition;
    }

    /// <summary>The section name this rule gates.</summary>
    public string Section { get; }

    /// <summary>The watched field's slash path.</summary>
    public string FieldPath { get; }

    /// <summary>The value the watched field must equal (when <see cref="Condition"/> is <see cref="CfCondition.Equals"/>).</summary>
    public object? EqualsValue { get; }

    /// <summary>How the watched value is compared.</summary>
    public CfCondition Condition { get; }

    /// <summary>The JsonForms effect (<c>ENABLE</c> / <c>SHOW</c>) applied while the condition holds.</summary>
    public abstract string Effect { get; }
}
