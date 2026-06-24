namespace ConfigForge.Core.Schema;

/// <summary>
/// Parses a combined ConfigForge schema document
/// (<c>{ "schema": …, "uiSchema": …, "x-cf": … }</c>) into a resolved
/// <see cref="ConfigSchema"/>.
/// </summary>
public interface IJsonFormsSchemaParser
{
    /// <summary>Parses a combined schema document from a JSON string.</summary>
    /// <param name="json">The combined schema JSON.</param>
    /// <returns>The resolved schema.</returns>
    /// <exception cref="SchemaParseException">The document is malformed or invalid.</exception>
    ConfigSchema Parse(string json);

    /// <summary>Parses a combined schema document from a file.</summary>
    /// <param name="path">The path to the combined schema JSON file.</param>
    /// <returns>The resolved schema.</returns>
    /// <exception cref="SchemaParseException">The file is missing, malformed, or invalid.</exception>
    ConfigSchema ParseFromFile(string path);
}
