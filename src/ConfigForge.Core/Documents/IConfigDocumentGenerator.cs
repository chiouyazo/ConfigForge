using ConfigForge.Abstractions;
using ConfigForge.Core.Schema;

namespace ConfigForge.Core.Documents;

/// <summary>Generates seed documents from a resolved schema.</summary>
public interface IConfigDocumentGenerator
{
    /// <summary>
    /// Generates a fully populated example document: every field receives a
    /// value that validates against the schema.
    /// </summary>
    /// <param name="schema">The schema to generate from.</param>
    /// <returns>An example document.</returns>
    ConfigDocument GenerateExample(ConfigSchema schema);

    /// <summary>
    /// Generates a minimal document containing only fields that declare a default
    /// value.
    /// </summary>
    /// <param name="schema">The schema to generate from.</param>
    /// <returns>An empty (defaults-only) document.</returns>
    ConfigDocument GenerateEmpty(ConfigSchema schema);
}
