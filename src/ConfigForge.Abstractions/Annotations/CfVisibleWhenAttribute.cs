namespace ConfigForge.Abstractions.Annotations;

/// <summary>
/// Shows this field only while another field equals a value; otherwise it is hidden.
/// </summary>
/// <example>
/// <code>
/// [CfVisibleWhen("mode", "advanced")]       // shown only when mode == "advanced"
/// public int? Threshold { get; init; }
/// </code>
/// </example>
[AttributeUsage(
    AttributeTargets.Property | AttributeTargets.Field,
    AllowMultiple = false,
    Inherited = false
)]
public sealed class CfVisibleWhenAttribute : CfRuleAttribute
{
    /// <summary>Shows the field when the watched boolean field is <c>true</c>.</summary>
    /// <param name="fieldPath">Slash path to the watched field.</param>
    public CfVisibleWhenAttribute(string fieldPath)
        : base(fieldPath, true) { }

    /// <summary>Shows the field when the watched field equals <paramref name="equalsValue"/>.</summary>
    /// <param name="fieldPath">Slash path to the watched field.</param>
    /// <param name="equalsValue">The value that shows this field.</param>
    public CfVisibleWhenAttribute(string fieldPath, object equalsValue)
        : base(fieldPath, equalsValue) { }

    /// <summary>Shows the field based on the watched field's presence (e.g. is-not-empty).</summary>
    /// <param name="fieldPath">Slash path to the watched field.</param>
    /// <param name="condition">The presence comparison (e.g. <see cref="CfCondition.IsNotEmpty"/>).</param>
    public CfVisibleWhenAttribute(string fieldPath, CfCondition condition)
        : base(fieldPath, condition) { }

    /// <inheritdoc />
    public override string Effect => "SHOW";
}
