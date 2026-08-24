using System.Text.Json;
using ConfigForge.Core.Internal;

namespace ConfigForge.Core.Documents;

/// <summary>
/// Converts a single document value between its in-memory shape (nested dictionaries, lists and
/// scalars) and JSON text. Used by the <c>code</c> control so an object-valued field edits as JSON
/// instead of rendering its CLR type name.
/// </summary>
public static class ConfigValueJson
{
    private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };

    /// <summary>Serializes a document value to indented JSON text.</summary>
    public static string Serialize(object? value) =>
        JsonValueHelper.ToJsonNode(value)?.ToJsonString(IndentedOptions) ?? "null";

    /// <summary>Parses JSON text into a document value. Throws <see cref="JsonException"/> on invalid JSON.</summary>
    public static object? Parse(string json)
    {
        using JsonDocument parsed = JsonDocument.Parse(json);
        return JsonValueHelper.FromElement(parsed.RootElement);
    }
}
