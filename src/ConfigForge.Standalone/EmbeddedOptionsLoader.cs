using System.IO;
using System.Reflection;

namespace ConfigForge.Standalone;

/// <summary>
/// Loads the <see cref="StandaloneConfigForgeOptions"/> and the embedded schema
/// JSON for the running host.
/// </summary>
public static class EmbeddedOptionsLoader
{
    /// <summary>
    /// The logical-name suffix the build assigns to embedded schema resources.
    /// </summary>
    private const string SchemaResourceSuffix = "schemas/example-product.json";

    /// <summary>
    /// Returns the host options.
    /// </summary>
    /// <remarks>
    /// The embedded schema is packed under the <c>schemas/</c> logical-name prefix
    /// (see the csproj). <see cref="StandaloneConfigForgeOptions.EmbeddedSchemaId"/>
    /// records the resource the host renders against.
    /// </remarks>
    /// <returns>The resolved host options.</returns>
    public static StandaloneConfigForgeOptions Load() =>
        new() { EmbeddedSchemaId = SchemaResourceSuffix };

    /// <summary>
    /// Reads the combined ConfigForge schema document embedded in this assembly.
    /// </summary>
    /// <remarks>
    /// Looks for a manifest resource whose name ends with
    /// <c>schemas/example-product.json</c>; failing that, the first resource under
    /// any <c>schemas/</c> prefix ending in <c>.json</c>. Returns <see langword="null"/>
    /// when no embedded schema is present so the host can still launch.
    /// </remarks>
    /// <returns>The schema JSON, or <see langword="null"/> if none is embedded.</returns>
    public static string? LoadSchemaJson()
    {
        Assembly assembly = typeof(EmbeddedOptionsLoader).Assembly;
        string[] names = assembly.GetManifestResourceNames();

        string? resourceName =
            Array.Find(
                names,
                n => n.EndsWith(SchemaResourceSuffix, StringComparison.OrdinalIgnoreCase)
            )
            ?? Array.Find(
                names,
                n =>
                    n.Contains("schemas/", StringComparison.OrdinalIgnoreCase)
                    && n.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            );

        if (resourceName is null)
        {
            return null;
        }

        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
