using System.Text.Json.Nodes;
using ConfigForge.Abstractions;
using ConfigForge.Core.Internal;
using Json.Schema;

namespace ConfigForge.Core.Schema;

/// <summary>
/// Evaluates a JsonForms rule by validating the scoped document value against the
/// rule's condition schema.
/// </summary>
public sealed class JsonFormsRuleEvaluator : IJsonFormsRuleEvaluator
{
    /// <inheritdoc />
    public RuleEffect Evaluate(JsonFormsRule rule, ConfigDocument document)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(document);

        if (rule.Condition.Schema is null)
        {
            return rule.Effect;
        }

        string? key = ScopeToKey(rule.Condition.Scope);
        object? value = key is not null && document.TryGetValue(key, out object? raw) ? raw : null;
        JsonNode? valueNode = JsonValueHelper.ToJsonNode(value);

        JsonSchema conditionSchema;
        try
        {
            conditionSchema = JsonSchema.FromText(rule.Condition.Schema.ToJsonString());
        }
        catch (System.Text.Json.JsonException)
        {
            return RuleEffect.None;
        }

        EvaluationResults results = conditionSchema.Evaluate(valueNode, FieldSchemaBuilder.Options);
        return results.IsValid ? rule.Effect : RuleEffect.None;
    }

    private static string? ScopeToKey(string scope)
    {
        const string prefix = "#/properties/";
        return scope.StartsWith(prefix, StringComparison.Ordinal) ? scope[prefix.Length..] : null;
    }
}
