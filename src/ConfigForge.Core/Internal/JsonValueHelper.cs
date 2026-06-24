using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ConfigForge.Core.Internal;

/// <summary>
/// Shared helpers for converting between JSON representations and the loosely
/// typed CLR values stored in a <see cref="Abstractions.ConfigDocument"/>.
/// </summary>
internal static class JsonValueHelper
{
    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        WriteIndented = false,
    };

    /// <summary>
    /// Converts a <see cref="JsonElement"/> into a stable CLR value: strings,
    /// booleans, longs/doubles for numbers, lists, and dictionaries. The result
    /// round-trips through <see cref="ToJsonNode"/> and <see cref="Serialize"/>.
    /// </summary>
    public static object? FromElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null;
            case JsonValueKind.Number:
                if (element.TryGetInt64(out long l))
                {
                    return l;
                }

                return element.GetDouble();
            case JsonValueKind.Array:
                List<object?> list = [];
                foreach (JsonElement item in element.EnumerateArray())
                {
                    list.Add(FromElement(item));
                }

                return list;
            case JsonValueKind.Object:
                Dictionary<string, object?> map = new(StringComparer.Ordinal);
                foreach (JsonProperty prop in element.EnumerateObject())
                {
                    map[prop.Name] = FromElement(prop.Value);
                }

                return map;
            default:
                return null;
        }
    }

    /// <summary>Converts a stored CLR value into a <see cref="JsonNode"/>.</summary>
    public static JsonNode? ToJsonNode(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case JsonNode node:
                return node.DeepClone();
            case JsonElement element:
                return JsonSerializer.SerializeToNode(element);
            default:
                return JsonSerializer.SerializeToNode(value, value.GetType());
        }
    }

    /// <summary>
    /// Produces a canonical, comparable JSON string for a stored value. Used for
    /// equality checks in diffing and dirty-state tracking.
    /// </summary>
    public static string Serialize(object? value)
    {
        return value switch
        {
            null => "null",
            JsonNode node => node.ToJsonString(CanonicalOptions),
            JsonElement element => element.GetRawText(),
            string s => JsonSerializer.Serialize(s, CanonicalOptions),
            _ => JsonSerializer.Serialize(value, value.GetType(), CanonicalOptions),
        };
    }

    /// <summary>Formats a CLR value for use as a date/time string in generated examples.</summary>
    public static string FormatInvariant(IFormattable value, string format) =>
        value.ToString(format, CultureInfo.InvariantCulture);
}
