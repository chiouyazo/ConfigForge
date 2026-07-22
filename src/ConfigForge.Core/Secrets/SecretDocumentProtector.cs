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
/// field whose control type is <c>secret</c>. Secret controls are single string values, so
/// only object navigation is performed; a path that does not resolve to an object property
/// is left untouched.
/// </para>
/// </remarks>
public static class SecretDocumentProtector
{
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
            string[] segments = path.Split('/');
            if (
                TryGetParent(root, segments, out JsonObject? parent)
                && parent![segments[^1]] is JsonValue value
                && value.TryGetValue(out string? stored)
                && !string.IsNullOrEmpty(stored)
            )
            {
                parent[segments[^1]] = ConfigForgeSecret.StoredMarker;
            }
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
        JsonObject? stored = storedJson is null ? null : Parse(storedJson);

        foreach (string path in secretPaths)
        {
            string[] segments = path.Split('/');
            if (!TryGetParent(incoming, segments, out JsonObject? parent))
            {
                continue;
            }

            string leaf = segments[^1];
            JsonNode? node = parent![leaf];
            string? value = node is JsonValue jv && jv.TryGetValue(out string? s) ? s : null;

            if (node is null || string.IsNullOrEmpty(value))
            {
                // Cleared (or never set): do not persist an empty secret.
                parent.Remove(leaf);
            }
            else if (string.Equals(value, ConfigForgeSecret.StoredMarker, StringComparison.Ordinal))
            {
                // Untouched: carry the stored value across unchanged, or drop it if none exists.
                string? current = StoredValue(stored, segments);
                if (current is null)
                {
                    parent.Remove(leaf);
                }
                else
                {
                    parent[leaf] = current;
                }
            }
            else if (!protector.IsProtected(value))
            {
                // Newly entered plaintext: encrypt it. An already-protected value is left as is.
                parent[leaf] = protector.Protect(value);
            }
        }

        return incoming.ToJsonString();
    }

    private static string? StoredValue(JsonObject? stored, string[] segments)
    {
        if (
            stored is not null
            && TryGetParent(stored, segments, out JsonObject? parent)
            && parent![segments[^1]] is JsonValue value
            && value.TryGetValue(out string? current)
        )
        {
            return current;
        }

        return null;
    }

    private static bool TryGetParent(JsonObject root, string[] segments, out JsonObject? parent)
    {
        JsonObject current = root;
        for (int i = 0; i < segments.Length - 1; i++)
        {
            if (current[segments[i]] is JsonObject next)
            {
                current = next;
            }
            else
            {
                parent = null;
                return false;
            }
        }

        parent = current;
        return true;
    }

    private static JsonObject Parse(string json) =>
        JsonNode.Parse(json) as JsonObject
        ?? throw new JsonException("The configuration document is not a JSON object.");
}
