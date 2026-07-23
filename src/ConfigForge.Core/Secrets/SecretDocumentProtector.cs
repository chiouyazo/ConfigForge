using System.Text.Json;
using System.Text.Json.Nodes;
using ConfigForge.Abstractions;

namespace ConfigForge.Core.Secrets;

/// <summary>
/// Applies the write-only secret convention (<see cref="ConfigForgeSecret"/>) to a
/// configuration document at the transport boundary, using an
/// <see cref="IConfigSecretProtector"/> to encrypt values at rest.
/// </summary>
/// <remarks>
/// <para>
/// A host wraps its load/save with this: on load it <see cref="Redact"/>s the stored
/// document so no plaintext (nor ciphertext) secret reaches the editor, and on save it
/// <see cref="Merge"/>s the editor's document against what is currently stored, so an
/// untouched secret keeps its stored value, a newly typed value is encrypted, and a
/// cleared value is removed.
/// </para>
/// <para>
/// Secret paths are <c>/</c>-separated property paths into the (nested) document, one per
/// field whose control type is <c>secret</c>. A <c>*</c> segment fans the remaining path
/// out over every element of an array or every value of a map, so secrets nested inside
/// collections (for example <c>items/*/token</c>) are protected too. Element/value
/// matching between the incoming and stored documents is by array index and by map key.
/// </para>
/// </remarks>
public static class SecretDocumentProtector
{
    private const string Wildcard = "*";

    /// <summary>
    /// Returns <paramref name="documentJson"/> with every present secret value replaced by
    /// <see cref="ConfigForgeSecret.StoredMarker"/>, so the editor learns a secret is set
    /// without ever receiving its stored value.
    /// </summary>
    /// <param name="documentJson">The stored document JSON.</param>
    /// <param name="secretPaths">The secret field paths.</param>
    /// <returns>The redacted document JSON.</returns>
    public static string Redact(string documentJson, IEnumerable<string> secretPaths)
    {
        ArgumentNullException.ThrowIfNull(documentJson);
        ArgumentNullException.ThrowIfNull(secretPaths);

        JsonObject root = Parse(documentJson);
        foreach (string path in secretPaths)
        {
            RedactPath(root, path.Split('/'), 0);
        }

        return root.ToJsonString();
    }

    /// <summary>
    /// Returns the editor's <paramref name="incomingJson"/> with each secret field resolved
    /// against <paramref name="storedJson"/>: the <see cref="ConfigForgeSecret.StoredMarker"/>
    /// keeps the currently stored value, an empty or null value clears it, and any other value
    /// is treated as new plaintext and encrypted.
    /// </summary>
    /// <param name="protector">The cipher used to encrypt newly entered plaintext secrets.</param>
    /// <param name="incomingJson">The document JSON posted by the editor.</param>
    /// <param name="storedJson">The currently stored document JSON, or null when none exists.</param>
    /// <param name="secretPaths">The secret field paths.</param>
    /// <returns>The merged document JSON with secrets protected at rest.</returns>
    public static string Merge(
        IConfigSecretProtector protector,
        string incomingJson,
        string? storedJson,
        IEnumerable<string> secretPaths
    )
    {
        ArgumentNullException.ThrowIfNull(protector);
        ArgumentNullException.ThrowIfNull(incomingJson);
        ArgumentNullException.ThrowIfNull(secretPaths);

        JsonObject incoming = Parse(incomingJson);
        JsonObject? stored = storedJson is null ? null : JsonNode.Parse(storedJson) as JsonObject;

        foreach (string path in secretPaths)
        {
            MergePath(protector, incoming, stored, path.Split('/'), 0);
        }

        return incoming.ToJsonString();
    }

    private static void RedactPath(JsonNode? node, string[] segments, int index)
    {
        if (node is null)
        {
            return;
        }

        string segment = segments[index];
        bool last = index == segments.Length - 1;

        if (string.Equals(segment, Wildcard, StringComparison.Ordinal))
        {
            if (node is JsonArray array)
            {
                for (int i = 0; i < array.Count; i++)
                {
                    if (last)
                    {
                        if (ShouldRedact(array[i]))
                        {
                            // Indexed so a kept element resolves back to its stored value even
                            // after the list is reordered or an element is removed.
                            array[i] = ConfigForgeSecret.IndexedMarker(i);
                        }
                    }
                    else
                    {
                        RedactPath(array[i], segments, index + 1);
                    }
                }
            }
            else if (node is JsonObject map)
            {
                foreach (string key in map.Select(entry => entry.Key).ToList())
                {
                    if (last)
                    {
                        if (ShouldRedact(map[key]))
                        {
                            map[key] = ConfigForgeSecret.StoredMarker;
                        }
                    }
                    else
                    {
                        RedactPath(map[key], segments, index + 1);
                    }
                }
            }

            return;
        }

        if (node is not JsonObject obj)
        {
            return;
        }

        if (last)
        {
            if (ShouldRedact(obj[segment]))
            {
                obj[segment] = ConfigForgeSecret.StoredMarker;
            }
        }
        else
        {
            RedactPath(obj[segment], segments, index + 1);
        }
    }

    private static void MergePath(
        IConfigSecretProtector protector,
        JsonNode? incoming,
        JsonNode? stored,
        string[] segments,
        int index
    )
    {
        if (incoming is null)
        {
            return;
        }

        string segment = segments[index];
        bool last = index == segments.Length - 1;

        if (string.Equals(segment, Wildcard, StringComparison.Ordinal))
        {
            if (incoming is JsonArray array)
            {
                JsonArray? storedArray = stored as JsonArray;
                if (last)
                {
                    // A secret array leaf: rebuild it so removed elements drop out and kept ones
                    // resolve by their recorded index (see MergeSecretArrayLeaf).
                    MergeSecretArrayLeaf(protector, array, storedArray);
                }
                else
                {
                    for (int i = 0; i < array.Count; i++)
                    {
                        JsonNode? storedItem =
                            storedArray is not null && i < storedArray.Count
                                ? storedArray[i]
                                : null;
                        MergePath(protector, array[i], storedItem, segments, index + 1);
                    }
                }
            }
            else if (incoming is JsonObject map)
            {
                JsonObject? storedMap = stored as JsonObject;
                foreach (string key in map.Select(entry => entry.Key).ToList())
                {
                    if (last)
                    {
                        ApplyInObject(protector, map, key, storedMap?[key]);
                    }
                    else
                    {
                        MergePath(protector, map[key], storedMap?[key], segments, index + 1);
                    }
                }
            }

            return;
        }

        if (incoming is not JsonObject obj)
        {
            return;
        }

        JsonObject? storedObj = stored as JsonObject;
        if (last)
        {
            ApplyInObject(protector, obj, segment, storedObj?[segment]);
        }
        else
        {
            MergePath(protector, obj[segment], storedObj?[segment], segments, index + 1);
        }
    }

    private static void ApplyInObject(
        IConfigSecretProtector protector,
        JsonObject parent,
        string key,
        JsonNode? stored
    )
    {
        (bool remove, JsonNode? replacement) = Resolve(protector, parent[key], stored);
        if (remove)
        {
            parent.Remove(key);
        }
        else if (replacement is not null)
        {
            parent[key] = replacement;
        }
    }

    /// <summary>
    /// Rebuilds a secret array leaf: a removed or emptied element drops out, a kept element (a
    /// stored marker) resolves to its stored value by the index the marker records (falling back
    /// to its current position for a plain marker), and any new plaintext is encrypted. Rebuilding
    /// rather than editing in place is what makes removing a middle element correct: kept markers
    /// still carry their original stored index, so no element is paired with the wrong ciphertext.
    /// </summary>
    private static void MergeSecretArrayLeaf(
        IConfigSecretProtector protector,
        JsonArray incoming,
        JsonArray? stored
    )
    {
        List<JsonNode?> resolved = [];
        for (int i = 0; i < incoming.Count; i++)
        {
            string? value = AsString(incoming[i]);

            if (ConfigForgeSecret.IsStoredMarker(value))
            {
                int storedIndex = ConfigForgeSecret.TryGetStoredIndex(value, out int parsed)
                    ? parsed
                    : i;
                if (
                    stored is not null
                    && storedIndex >= 0
                    && storedIndex < stored.Count
                    && AsString(stored[storedIndex]) is { } keptValue
                )
                {
                    resolved.Add(JsonValue.Create(keptValue));
                }

                // A marker whose stored value is gone is simply dropped.
                continue;
            }

            if (string.IsNullOrEmpty(value))
            {
                // Removed element.
                continue;
            }

            resolved.Add(
                JsonValue.Create(protector.IsProtected(value) ? value : protector.Protect(value))
            );
        }

        incoming.Clear();
        foreach (JsonNode? node in resolved)
        {
            incoming.Add(node);
        }
    }

    private static (bool Remove, JsonNode? Replacement) Resolve(
        IConfigSecretProtector protector,
        JsonNode? incoming,
        JsonNode? stored
    )
    {
        string? value = AsString(incoming);

        if (string.Equals(value, ConfigForgeSecret.StoredMarker, StringComparison.Ordinal))
        {
            string? current = AsString(stored);
            return current is null ? (true, null) : (false, JsonValue.Create(current));
        }

        if (string.IsNullOrEmpty(value))
        {
            return (true, null);
        }

        return protector.IsProtected(value)
            ? (false, null)
            : (false, JsonValue.Create(protector.Protect(value)));
    }

    private static bool ShouldRedact(JsonNode? node) => !string.IsNullOrEmpty(AsString(node));

    private static string? AsString(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue(out string? text) ? text : null;

    private static JsonObject Parse(string json) =>
        JsonNode.Parse(json) as JsonObject
        ?? throw new JsonException("The configuration document is not a JSON object.");
}
