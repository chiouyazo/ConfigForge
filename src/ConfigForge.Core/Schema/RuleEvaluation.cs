using System.Text.Json.Nodes;
using ConfigForge.Abstractions;

namespace ConfigForge.Core.Schema;

/// <summary>
/// Folds a set of JsonForms rules into a (visible, enabled) state against the live document.
/// Two-directional and spec-aligned: <c>ENABLE</c>/<c>SHOW</c> apply their effect only while the
/// condition holds and revert it otherwise (so <c>ENABLE</c> disables when the condition fails),
/// <c>DISABLE</c>/<c>HIDE</c> the reverse. Multiple rules combine with AND. Shared by every place
/// that gates on rules: fields, the layout control renderer, and categories.
/// </summary>
public static class RuleEvaluation
{
    /// <summary>Resolves the (visible, enabled) state from a set of rules.</summary>
    /// <param name="rules">The rules to fold.</param>
    /// <param name="evaluator">The rule evaluator.</param>
    /// <param name="document">The live document the conditions read from.</param>
    /// <returns>Whether the target should be visible, and whether it should be enabled.</returns>
    public static (bool Visible, bool Enabled) Resolve(
        IEnumerable<JsonFormsRule> rules,
        IJsonFormsRuleEvaluator evaluator,
        ConfigDocument document
    )
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(evaluator);

        bool visible = true;
        bool enabled = true;
        foreach (JsonFormsRule rule in rules)
        {
            bool matches = evaluator.Evaluate(rule, document) != RuleEffect.None;
            switch (rule.Effect)
            {
                case RuleEffect.Enable:
                    enabled &= matches;
                    break;
                case RuleEffect.Disable:
                    enabled &= !matches;
                    break;
                case RuleEffect.Show:
                    visible &= matches;
                    break;
                case RuleEffect.Hide:
                    visible &= !matches;
                    break;
                default:
                    break;
            }
        }

        return (visible, enabled);
    }

    /// <summary>
    /// Resolves rules whose condition scopes are relative to a container (e.g. a map/oneof entry),
    /// rebasing each scope by <paramref name="scopePrefix"/> (a key like <c>connectors/{guid}</c>) before
    /// evaluating. Used to gate a section/sub-tab inside an entry against a field within that entry.
    /// </summary>
    public static (bool Visible, bool Enabled) Resolve(
        IEnumerable<JsonFormsRule> rules,
        IJsonFormsRuleEvaluator evaluator,
        ConfigDocument document,
        string scopePrefix
    )
    {
        ArgumentNullException.ThrowIfNull(rules);
        return Resolve(rules.Select(r => RebaseScope(r, scopePrefix)), evaluator, document);
    }

    private static JsonFormsRule RebaseScope(JsonFormsRule rule, string scopePrefix)
    {
        string? key = JsonFormsScope.ToKey(rule.Condition.Scope);
        if (key is null || scopePrefix.Length == 0)
        {
            return rule;
        }

        return new JsonFormsRule
        {
            Effect = rule.Effect,
            Condition = new RuleCondition
            {
                Scope = JsonFormsScope.ToScope(JsonFormsScope.JoinKey(scopePrefix, key)),
                Schema = rule.Condition.Schema?.DeepClone(),
            },
        };
    }
}
