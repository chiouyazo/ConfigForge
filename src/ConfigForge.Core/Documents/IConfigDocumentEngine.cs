using ConfigForge.Abstractions;
using ConfigForge.Core.Schema;

namespace ConfigForge.Core.Documents;

/// <summary>
/// Parses, serializes, and diffs configuration documents against a resolved
/// schema. <see cref="Parse"/> never throws.
/// </summary>
public interface IConfigDocumentEngine
{
    /// <summary>
    /// Parses raw configuration JSON against a schema. Never throws: malformed
    /// JSON is reported via <see cref="ConfigDocumentParseResult.JsonError"/>.
    /// </summary>
    /// <param name="json">The raw configuration JSON.</param>
    /// <param name="schema">The schema to validate against.</param>
    /// <returns>The parse result.</returns>
    ConfigDocumentParseResult Parse(string json, ConfigSchema schema);

    /// <summary>Serializes a document to indented JSON.</summary>
    /// <param name="document">The document to serialize.</param>
    /// <returns>The indented JSON representation.</returns>
    string Serialize(ConfigDocument document);

    /// <summary>Computes the differences between two documents.</summary>
    /// <param name="original">The baseline document.</param>
    /// <param name="modified">The modified document.</param>
    /// <returns>The set of changes.</returns>
    ConfigDiff Diff(ConfigDocument original, ConfigDocument modified);
}
