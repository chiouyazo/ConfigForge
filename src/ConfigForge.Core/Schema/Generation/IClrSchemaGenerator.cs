namespace ConfigForge.Core.Schema.Generation;

/// <summary>
/// Generates a combined ConfigForge schema document (<c>schema</c> + <c>x-cf</c>)
/// from a CLR type by reflection. The result is the same JSON shape that
/// <see cref="IJsonFormsSchemaParser"/> consumes, so it can be parsed directly,
/// served remotely, or written to disk.
/// </summary>
public interface IClrSchemaGenerator
{
    /// <summary>Generates the combined schema document for <paramref name="rootType"/>.</summary>
    /// <param name="rootType">The root configuration type to reflect over.</param>
    /// <param name="options">Generation options (id, naming, secrets, overlay).</param>
    /// <returns>The combined schema document as a JSON string.</returns>
    string Generate(Type rootType, SchemaGenerationOptions options);

    /// <summary>Generates the combined schema document for <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The root configuration type to reflect over.</typeparam>
    /// <param name="options">Generation options (id, naming, secrets, overlay).</param>
    /// <returns>The combined schema document as a JSON string.</returns>
    string Generate<T>(SchemaGenerationOptions options);
}
