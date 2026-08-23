namespace ConfigForge.Abstractions.Annotations;

/// <summary>
/// Shows a section (most visibly a <c>oneof</c> variant's sub-tab) only while a watched field
/// satisfies the condition; otherwise the sub-tab is hidden. Placed on the config type, repeatable.
/// </summary>
/// <example>
/// <code>
/// [CfSectionVisibleWhen("Advanced", "mode", "expert")]   // sub-tab shown only in expert mode
/// public sealed record ShopwareConfig : ShopConfig { … }
/// </code>
/// </example>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct,
    AllowMultiple = true,
    Inherited = false
)]
public sealed class CfSectionVisibleWhenAttribute : CfSectionRuleAttribute
{
    /// <summary>Shows the section while the watched boolean field is <c>true</c>.</summary>
    public CfSectionVisibleWhenAttribute(string section, string fieldPath)
        : base(section, fieldPath, true) { }

    /// <summary>Shows the section while the watched field equals <paramref name="equalsValue"/>.</summary>
    public CfSectionVisibleWhenAttribute(string section, string fieldPath, object equalsValue)
        : base(section, fieldPath, equalsValue) { }

    /// <summary>Shows the section based on the watched field's presence (e.g. is-set).</summary>
    public CfSectionVisibleWhenAttribute(string section, string fieldPath, CfCondition condition)
        : base(section, fieldPath, condition) { }

    /// <inheritdoc />
    public override string Effect => "SHOW";
}
