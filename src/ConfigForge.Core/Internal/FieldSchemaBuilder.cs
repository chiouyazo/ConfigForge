using System.Text.Json.Nodes;
using ConfigForge.Core.Schema;
using Json.Schema;

namespace ConfigForge.Core.Internal;

/// <summary>
/// Builds a Draft 7 <see cref="JsonSchema"/> for a single field from its
/// resolved <see cref="FieldDefinition.SchemaConstraints"/>.
/// </summary>
internal static class FieldSchemaBuilder
{
    private static readonly EvaluationOptions Draft7Options = new()
    {
        EvaluateAs = SpecVersion.Draft7,
        OutputFormat = OutputFormat.List,
    };

    /// <summary>The shared Draft 7 evaluation options.</summary>
    public static EvaluationOptions Options => Draft7Options;

    /// <summary>
    /// Builds a JSON Schema fragment from a field's constraints, or null when the
    /// field declares no constraints to validate against.
    /// </summary>
    public static JsonSchema? Build(FieldDefinition field)
    {
        if (field.SchemaConstraints.Count == 0)
        {
            return null;
        }

        JsonObject schemaNode = [];
        foreach (KeyValuePair<string, object> constraint in field.SchemaConstraints)
        {
            schemaNode[constraint.Key] = JsonValueHelper.ToJsonNode(constraint.Value);
        }

        return JsonSchema.FromText(schemaNode.ToJsonString());
    }
}
