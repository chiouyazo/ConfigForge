namespace ConfigForge.Core.Schema;

/// <summary>
/// The effect a JsonForms rule applies to its target element when the rule's
/// condition is satisfied.
/// </summary>
public enum RuleEffect
{
    /// <summary>No effect: the condition was not met (or there is no rule).</summary>
    None,

    /// <summary>Hide the target element.</summary>
    Hide,

    /// <summary>Show the target element.</summary>
    Show,

    /// <summary>Disable the target element.</summary>
    Disable,

    /// <summary>Enable the target element.</summary>
    Enable,
}
