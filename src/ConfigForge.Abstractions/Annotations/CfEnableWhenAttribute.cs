namespace ConfigForge.Abstractions.Annotations;

/// <summary>
/// Enables this field only while another field equals a value; otherwise it renders disabled.
/// </summary>
/// <example>
/// <code>
/// [CfEnableWhen("alerts/enabled")]          // enabled only when alerts.enabled == true
/// public string? AlertRecipient { get; init; }
///
/// [CfEnableWhen("mode", "advanced")]        // enabled only when mode == "advanced"
/// public int? Threshold { get; init; }
/// </code>
/// </example>
[AttributeUsage(
    AttributeTargets.Property | AttributeTargets.Field,
    AllowMultiple = false,
    Inherited = false
)]
public sealed class CfEnableWhenAttribute : CfRuleAttribute
{
    /// <summary>Enables the field when the watched boolean field is <c>true</c>.</summary>
    /// <param name="fieldPath">Slash path to the watched field.</param>
    public CfEnableWhenAttribute(string fieldPath)
        : base(fieldPath, true) { }

    /// <summary>Enables the field when the watched field equals <paramref name="equalsValue"/>.</summary>
    /// <param name="fieldPath">Slash path to the watched field.</param>
    /// <param name="equalsValue">The value that enables this field.</param>
    public CfEnableWhenAttribute(string fieldPath, object equalsValue)
        : base(fieldPath, equalsValue) { }

    /// <inheritdoc />
    public override string Effect => "ENABLE";
}
