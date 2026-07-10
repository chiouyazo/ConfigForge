using System.Text.Json;
using System.Text.Json.Nodes;

namespace ConfigForge.Core.Schema.Generation;

/// <summary>
/// Controls how <see cref="IClrSchemaGenerator"/> turns a CLR type into a combined
/// ConfigForge schema document.
/// </summary>
public sealed class SchemaGenerationOptions
{
    /// <summary>The schema id written to <c>x-cf.id</c>.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The display name written to <c>x-cf.name</c>.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The version written to <c>x-cf.version</c>.</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Naming policy applied to property names that carry no explicit
    /// <c>[JsonPropertyName]</c>. Defaults to camelCase to match System.Text.Json's
    /// common configuration.
    /// </summary>
    public JsonNamingPolicy PropertyNamingPolicy { get; set; } = JsonNamingPolicy.CamelCase;

    /// <summary>
    /// Naming policy applied to enum member names when emitting <c>enum</c> values.
    /// Defaults to camelCase to match a camelCase <c>JsonStringEnumConverter</c>.
    /// </summary>
    public JsonNamingPolicy EnumNamingPolicy { get; set; } = JsonNamingPolicy.CamelCase;

    /// <summary>
    /// Simple type names (no namespace) whose properties are treated as write-only
    /// secrets even without <c>[CfSecret]</c>. Empty by default; a host that uses library
    /// secret wrappers can register their type names here so they map to the secret
    /// control automatically.
    /// </summary>
    public ISet<string> SecretTypeNames { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Maximum recursion depth into nested types. Guards against cycles.</summary>
    public int MaxDepth { get; set; } = 32;

    /// <summary>When false (default), properties marked <c>[Obsolete]</c> are skipped.</summary>
    public bool IncludeObsolete { get; set; }

    /// <summary>
    /// Optional JSON fragment deep-merged over the generated document (mixed mode).
    /// Objects merge recursively; scalars and arrays from the overlay replace the
    /// generated value. Use it to hand-tune anything the reflection pass cannot infer.
    /// </summary>
    public JsonNode? Overlay { get; set; }
}
