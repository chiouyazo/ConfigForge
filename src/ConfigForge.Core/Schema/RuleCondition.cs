using System.Text.Json.Nodes;

namespace ConfigForge.Core.Schema;

/// <summary>
/// The condition portion of a JsonForms rule: a scoped value and the JSON Schema
/// fragment that value must match for the rule to take effect.
/// </summary>
public sealed class RuleCondition
{
    /// <summary>
    /// The JSON Pointer scope identifying the value to test, e.g.
    /// <c>#/properties/endpoint_url</c>.
    /// </summary>
    public string Scope { get; init; } = string.Empty;

    /// <summary>
    /// The JSON Schema fragment the scoped value is matched against. A null
    /// schema is treated as "always match".
    /// </summary>
    public JsonNode? Schema { get; init; }
}
