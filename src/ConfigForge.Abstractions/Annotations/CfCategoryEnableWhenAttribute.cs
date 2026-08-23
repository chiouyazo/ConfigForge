namespace ConfigForge.Abstractions.Annotations;

/// <summary>
/// Enables a whole category (tab) only while a watched field satisfies the condition; otherwise
/// the tab is shown but locked (not selectable). Placed on the config type, repeatable.
/// </summary>
/// <example>
/// <code>
/// // "Config" stays locked until a connection has been tested successfully (the test-connection
/// // action sets an untracked "connectionValid" field the rule watches).
/// [CfCategoryEnableWhen("Config", "connectionValid", CfCondition.IsSet)]
/// public sealed record ShopConfig { … }
/// </code>
/// </example>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct,
    AllowMultiple = true,
    Inherited = false
)]
public sealed class CfCategoryEnableWhenAttribute : CfCategoryRuleAttribute
{
    /// <summary>Enables the category while the watched boolean field is <c>true</c>.</summary>
    public CfCategoryEnableWhenAttribute(string category, string fieldPath)
        : base(category, fieldPath, true) { }

    /// <summary>Enables the category while the watched field equals <paramref name="equalsValue"/>.</summary>
    public CfCategoryEnableWhenAttribute(string category, string fieldPath, object equalsValue)
        : base(category, fieldPath, equalsValue) { }

    /// <summary>Enables the category based on the watched field's presence (e.g. is-set).</summary>
    public CfCategoryEnableWhenAttribute(string category, string fieldPath, CfCondition condition)
        : base(category, fieldPath, condition) { }

    /// <inheritdoc />
    public override string Effect => "ENABLE";
}
