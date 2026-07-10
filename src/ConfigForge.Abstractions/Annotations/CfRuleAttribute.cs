namespace ConfigForge.Abstractions.Annotations;

/// <summary>
/// Base for the conditional-rule attributes (<see cref="CfEnableWhenAttribute"/>,
/// <see cref="CfVisibleWhenAttribute"/>). Emitted by schema generation as an inline JsonForms
/// rule on the property, so the condition survives the overlay merge (unlike a hand-written
/// <c>uiSchema</c> rule, which the overlay would replace).
/// </summary>
public abstract class CfRuleAttribute : Attribute
{
    /// <summary>Creates a rule keyed off another field's value.</summary>
    /// <param name="fieldPath">
    /// Slash path to the field the condition watches (e.g. <c>alerts/enabled</c> or a nested
    /// <c>parent/child</c>), relative to the document root.
    /// </param>
    /// <param name="equalsValue">The value that field must equal for the effect to apply.</param>
    protected CfRuleAttribute(string fieldPath, object? equalsValue)
    {
        FieldPath = fieldPath;
        EqualsValue = equalsValue;
    }

    /// <summary>The watched field's slash path.</summary>
    public string FieldPath { get; }

    /// <summary>The value the watched field must equal.</summary>
    public object? EqualsValue { get; }

    /// <summary>The JsonForms effect (<c>ENABLE</c> / <c>SHOW</c>) when the condition matches.</summary>
    public abstract string Effect { get; }
}
