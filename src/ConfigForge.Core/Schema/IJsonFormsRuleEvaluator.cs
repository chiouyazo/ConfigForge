using ConfigForge.Abstractions;

namespace ConfigForge.Core.Schema;

/// <summary>Evaluates a JsonForms rule against the live document state.</summary>
public interface IJsonFormsRuleEvaluator
{
    /// <summary>
    /// Evaluates a rule: resolves the scoped value from the document and tests it
    /// against the condition schema.
    /// </summary>
    /// <param name="rule">The rule to evaluate.</param>
    /// <param name="document">The current document state.</param>
    /// <returns>
    /// The rule's <see cref="RuleEffect"/> when the condition matches; otherwise
    /// <see cref="RuleEffect.None"/>.
    /// </returns>
    RuleEffect Evaluate(JsonFormsRule rule, ConfigDocument document);
}
