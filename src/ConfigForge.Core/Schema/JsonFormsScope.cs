namespace ConfigForge.Core.Schema;

/// <summary>
/// Converts JsonForms <c>scope</c> pointers into the flat path keys used by
/// <see cref="FieldDefinition.Key"/> and <see cref="ConfigSchema.Fields"/>.
/// </summary>
public static class JsonFormsScope
{
    private const string PropertiesPrefix = "#/properties/";
    private const string PropertiesSeparator = "/properties/";

    /// <summary>
    /// Resolves a scope such as <c>#/properties/Parent/properties/Child</c> to the
    /// path key <c>Parent/Child</c>. Returns null when the scope is empty or does not
    /// address a property.
    /// </summary>
    /// <param name="scope">The JsonForms scope pointer.</param>
    /// <returns>The resolved path key, or null.</returns>
    public static string? ToKey(string? scope)
    {
        if (
            string.IsNullOrEmpty(scope)
            || !scope!.StartsWith(PropertiesPrefix, StringComparison.Ordinal)
        )
        {
            return null;
        }

        return scope[PropertiesPrefix.Length..]
            .Replace(PropertiesSeparator, "/", StringComparison.Ordinal);
    }

    /// <summary>Builds the canonical scope pointer for a path key.</summary>
    /// <param name="key">The path key, with <c>/</c> separating segments.</param>
    /// <returns>A JsonForms scope pointer.</returns>
    public static string ToScope(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return PropertiesPrefix + key.Replace("/", PropertiesSeparator, StringComparison.Ordinal);
    }
}
