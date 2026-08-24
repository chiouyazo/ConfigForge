namespace ConfigForge.Abstractions.Annotations;

/// <summary>
/// Enables a section (most visibly a <c>oneof</c> variant's sub-tab) only while a watched field
/// satisfies the condition; otherwise the sub-tab is shown but locked (not selectable). Placed on
/// the config type, repeatable.
/// </summary>
/// <example>
/// <code>
/// // An entry's "Config" and "Mapping" sub-tabs stay locked until the connection has been tested.
/// [CfSectionEnableWhen("Config", "connectionValid", CfCondition.IsSet)]
/// [CfSectionEnableWhen("Mapping", "connectionValid", CfCondition.IsSet)]
/// public sealed record HttpConnector : ConnectorConfig { … }
/// </code>
/// </example>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct,
    AllowMultiple = true,
    Inherited = false
)]
public sealed class CfSectionEnableWhenAttribute : CfSectionRuleAttribute
{
    /// <summary>Enables the section while the watched boolean field is <c>true</c>.</summary>
    public CfSectionEnableWhenAttribute(string section, string fieldPath)
        : base(section, fieldPath, true) { }

    /// <summary>Enables the section while the watched field equals <paramref name="equalsValue"/>.</summary>
    public CfSectionEnableWhenAttribute(string section, string fieldPath, object equalsValue)
        : base(section, fieldPath, equalsValue) { }

    /// <summary>Enables the section based on the watched field's presence (e.g. is-set).</summary>
    public CfSectionEnableWhenAttribute(string section, string fieldPath, CfCondition condition)
        : base(section, fieldPath, condition) { }

    /// <inheritdoc />
    public override string Effect => "ENABLE";
}
