namespace ConfigForge.Core.Schema;

/// <summary>
/// A parsed JsonForms rule: an <see cref="RuleEffect"/> applied when its
/// <see cref="Condition"/> is satisfied.
/// </summary>
public sealed class JsonFormsRule
{
    /// <summary>The effect applied when the condition matches.</summary>
    public RuleEffect Effect { get; init; }

    /// <summary>The condition that gates the effect.</summary>
    public RuleCondition Condition { get; init; } = new();
}
