namespace ConfigForge.Abstractions.Annotations;

/// <summary>
/// Base for the category-level conditional-rule attributes
/// (<see cref="CfCategoryEnableWhenAttribute"/>, <see cref="CfCategoryVisibleWhenAttribute"/>).
/// Placed on the config <b>type</b> (a category is shared by many properties) and keyed by the
/// category label. Emitted by schema generation into <c>x-cf.categories[label].rule</c>, so the
/// whole tab reacts to another field's value — e.g. keep a tab locked until a connection has been
/// tested (the "test" action writes a field the rule watches).
/// </summary>
public abstract class CfCategoryRuleAttribute : Attribute
{
    /// <summary>Creates a category rule that watches a field for equality.</summary>
    /// <param name="category">The category label (matches a <c>[CfGroup]</c>/<c>[CfCategory]</c>).</param>
    /// <param name="fieldPath">Slash path to the watched field, relative to the document root.</param>
    /// <param name="equalsValue">The value the watched field must equal.</param>
    protected CfCategoryRuleAttribute(string category, string fieldPath, object? equalsValue)
    {
        Category = category;
        FieldPath = fieldPath;
        EqualsValue = equalsValue;
        Condition = CfCondition.Equals;
    }

    /// <summary>Creates a category rule that watches a field for presence.</summary>
    /// <param name="category">The category label.</param>
    /// <param name="fieldPath">Slash path to the watched field.</param>
    /// <param name="condition">The presence comparison to apply.</param>
    protected CfCategoryRuleAttribute(string category, string fieldPath, CfCondition condition)
    {
        Category = category;
        FieldPath = fieldPath;
        EqualsValue = null;
        Condition = condition;
    }

    /// <summary>The category label this rule gates.</summary>
    public string Category { get; }

    /// <summary>The watched field's slash path.</summary>
    public string FieldPath { get; }

    /// <summary>The value the watched field must equal (when <see cref="Condition"/> is <see cref="CfCondition.Equals"/>).</summary>
    public object? EqualsValue { get; }

    /// <summary>How the watched value is compared.</summary>
    public CfCondition Condition { get; }

    /// <summary>The JsonForms effect (<c>ENABLE</c> / <c>SHOW</c>) applied while the condition holds.</summary>
    public abstract string Effect { get; }
}
