using ConfigForge.Abstractions;
using ConfigForge.Core.Schema;
using ConfigForge.Core.Secrets;

namespace ConfigForge.AspNet;

/// <summary>
/// Applies the write-only secret convention to configuration documents at the editor boundary,
/// driven by the schema's <c>secret</c> fields. When no <see cref="IConfigSecretProtector"/> is
/// registered it is a transparent pass-through, so hosts that do not encrypt secrets are unaffected.
/// </summary>
public sealed class ConfigSecretGateway
{
    private const string Separator = "/";
    private const string Wildcard = "*";
    private const string SecretControl = "secret";

    private readonly IConfigSecretProtector? _protector;

    /// <summary>Creates a gateway over the optionally registered secret cipher.</summary>
    /// <param name="protector">The secret cipher, or null when secret protection is disabled.</param>
    public ConfigSecretGateway(IConfigSecretProtector? protector = null)
    {
        _protector = protector;
    }

    /// <summary>Whether secret protection is active (a cipher is registered).</summary>
    public bool Enabled => _protector is not null;

    /// <summary>
    /// Hides stored secrets behind <see cref="ConfigForgeSecret.StoredMarker"/> before the document
    /// reaches the editor. Returns the input unchanged when protection is off or the schema has no
    /// secret fields.
    /// </summary>
    /// <param name="schema">The schema whose secret fields to redact.</param>
    /// <param name="documentJson">The stored document JSON.</param>
    /// <returns>The redacted document JSON.</returns>
    public string RedactForEditor(ConfigSchema schema, string documentJson)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(documentJson);

        if (_protector is null)
        {
            return documentJson;
        }

        List<string> paths = SecretPaths(schema);
        return paths.Count == 0
            ? documentJson
            : SecretDocumentProtector.Redact(documentJson, paths);
    }

    /// <summary>
    /// Resolves each secret field of the editor's document against what is currently stored (keep,
    /// replace-and-encrypt, or clear) before it is persisted. Returns the input unchanged when
    /// protection is off or the schema has no secret fields.
    /// </summary>
    /// <param name="schema">The schema whose secret fields to resolve.</param>
    /// <param name="incomingJson">The document JSON posted by the editor.</param>
    /// <param name="storedJson">The currently stored document JSON, or null when none exists.</param>
    /// <returns>The document JSON to persist, with secrets protected at rest.</returns>
    public string MergeForStore(ConfigSchema schema, string incomingJson, string? storedJson)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(incomingJson);

        if (_protector is null)
        {
            return incomingJson;
        }

        List<string> paths = SecretPaths(schema);
        return paths.Count == 0
            ? incomingJson
            : SecretDocumentProtector.Merge(_protector, incomingJson, storedJson, paths);
    }

    private static List<string> SecretPaths(ConfigSchema schema)
    {
        List<string> paths = [];
        foreach (FieldDefinition field in schema.Fields.Values)
        {
            CollectSecretPaths(field, field.Key, paths);
        }

        return paths;
    }

    private static void CollectSecretPaths(FieldDefinition field, string path, List<string> paths)
    {
        if (string.Equals(field.ControlType, SecretControl, StringComparison.Ordinal))
        {
            paths.Add(path);
            return;
        }

        foreach (FieldDefinition child in field.Children)
        {
            CollectSecretPaths(child, path + Separator + Wildcard + Separator + child.Key, paths);
        }

        if (field.ValueField is not null)
        {
            string valuePath = path + Separator + Wildcard;
            if (
                string.Equals(field.ValueField.ControlType, SecretControl, StringComparison.Ordinal)
            )
            {
                paths.Add(valuePath);
            }
            else
            {
                foreach (FieldDefinition child in field.ValueField.Children)
                {
                    CollectSecretPaths(child, valuePath + Separator + child.Key, paths);
                }
            }
        }

        foreach (OneOfVariant variant in field.OneOfVariants)
        {
            foreach (FieldDefinition child in variant.Children)
            {
                CollectSecretPaths(child, path + Separator + child.Key, paths);
            }
        }
    }
}
