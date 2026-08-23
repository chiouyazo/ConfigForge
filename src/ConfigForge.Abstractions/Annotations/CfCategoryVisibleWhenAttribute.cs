namespace ConfigForge.Abstractions.Annotations;

/// <summary>
/// Shows a whole category (tab) only while a watched field satisfies the condition; otherwise the
/// tab is hidden from the sidebar. Placed on the config type, repeatable.
/// </summary>
/// <example>
/// <code>
/// [CfCategoryVisibleWhen("Advanced", "mode", "expert")]   // tab shown only in expert mode
/// public sealed record AppConfig { … }
/// </code>
/// </example>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct,
    AllowMultiple = true,
    Inherited = false
)]
public sealed class CfCategoryVisibleWhenAttribute : CfCategoryRuleAttribute
{
    /// <summary>Shows the category while the watched boolean field is <c>true</c>.</summary>
    public CfCategoryVisibleWhenAttribute(string category, string fieldPath)
        : base(category, fieldPath, true) { }

    /// <summary>Shows the category while the watched field equals <paramref name="equalsValue"/>.</summary>
    public CfCategoryVisibleWhenAttribute(string category, string fieldPath, object equalsValue)
        : base(category, fieldPath, equalsValue) { }

    /// <summary>Shows the category based on the watched field's presence (e.g. is-set).</summary>
    public CfCategoryVisibleWhenAttribute(string category, string fieldPath, CfCondition condition)
        : base(category, fieldPath, condition) { }

    /// <inheritdoc />
    public override string Effect => "SHOW";
}
