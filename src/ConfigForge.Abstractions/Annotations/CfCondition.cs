namespace ConfigForge.Abstractions.Annotations;

/// <summary>
/// How a conditional-rule attribute (<see cref="CfEnableWhenAttribute"/> /
/// <see cref="CfVisibleWhenAttribute"/>) compares the watched field's value.
/// </summary>
public enum CfCondition
{
    /// <summary>The watched value must equal the supplied value (the default).</summary>
    Equals,

    /// <summary>The watched value must be present (not null / not missing).</summary>
    IsSet,

    /// <summary>The watched value must be present and not an empty string.</summary>
    IsNotEmpty,
}
