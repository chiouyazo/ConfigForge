using ConfigForge.Abstractions;

namespace ConfigForge.Core.Documents;

/// <summary>
/// The outcome of parsing raw configuration JSON against a schema. Parsing never
/// throws: a malformed document yields an empty <see cref="Document"/> with
/// <see cref="JsonError"/> set.
/// </summary>
public sealed class ConfigDocumentParseResult
{
    /// <summary>The parsed document. Always non-null; empty on a JSON error.</summary>
    public ConfigDocument Document { get; init; } = new();

    /// <summary>Keys present in the document that are not declared by the schema.</summary>
    public IReadOnlyList<string> UnknownKeys { get; init; } = [];

    /// <summary>Required field keys that were absent from the document.</summary>
    public IReadOnlyList<string> MissingRequiredKeys { get; init; } = [];

    /// <summary>Present fields whose value failed schema-constraint validation.</summary>
    public IReadOnlyList<ValidationError> InvalidValues { get; init; } = [];

    /// <summary>
    /// Documented extension: non-null when the raw JSON was malformed and could
    /// not be parsed at all. Distinct from per-field validation failures.
    /// </summary>
    public string? JsonError { get; init; }

    /// <summary>
    /// True when the document is well-formed JSON with all required keys present
    /// and no invalid values. Unknown keys do not invalidate the document.
    /// </summary>
    public bool IsValid =>
        JsonError is null && MissingRequiredKeys.Count == 0 && InvalidValues.Count == 0;
}
