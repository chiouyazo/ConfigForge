using System.Globalization;
using ConfigForge.Abstractions;

namespace ConfigForge.Core.Schema;

/// <summary>
/// Walks a schema against a live document, yielding every reachable field at its concrete
/// document key — descending into map entries (per key), oneof variants (by the active
/// discriminator), array items (per index), and nested objects. This gives the flat key-space
/// callers (required validation, untracked stripping) the entry/variant paths they otherwise miss.
/// </summary>
public static class SchemaWalker
{
    /// <summary>Yields every (concrete document key, field template) pair reachable in the document.</summary>
    public static IEnumerable<(string Key, FieldDefinition Field)> EnumerateFields(
        ConfigSchema schema,
        ConfigDocument document
    )
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(document);
        return Enumerate(schema, document);
    }

    private static IEnumerable<(string Key, FieldDefinition Field)> Enumerate(
        ConfigSchema schema,
        ConfigDocument document
    )
    {
        foreach (FieldDefinition field in schema.Fields.Values)
        {
            foreach (
                (string Key, FieldDefinition Field) pair in Descend(field, field.Key, document)
            )
            {
                yield return pair;
            }
        }
    }

    /// <summary>The concrete keys of required fields absent from the document (recursively).</summary>
    public static IReadOnlyList<string> MissingRequiredKeys(
        ConfigSchema schema,
        ConfigDocument document
    ) =>
        [
            .. EnumerateFields(schema, document)
                .Where(p => p.Field.Required && !document.ContainsKey(p.Key))
                .Select(p => p.Key),
        ];

    /// <summary>The concrete keys of untracked fields present in the document (recursively).</summary>
    public static IReadOnlyList<string> UntrackedKeys(
        ConfigSchema schema,
        ConfigDocument document
    ) =>
        [
            .. EnumerateFields(schema, document)
                .Where(p => !p.Field.Tracked && document.ContainsKey(p.Key))
                .Select(p => p.Key),
        ];

    private static IEnumerable<(string Key, FieldDefinition Field)> Descend(
        FieldDefinition field,
        string key,
        ConfigDocument document
    )
    {
        yield return (key, field);

        switch (field.ControlType)
        {
            case "map":
                if (
                    field.ValueField is { } valueTemplate
                    && document[key] is IDictionary<string, object?> map
                )
                {
                    foreach (string entryKey in map.Keys)
                    {
                        foreach (
                            (string Key, FieldDefinition Field) pair in Descend(
                                valueTemplate,
                                JsonFormsScope.JoinKey(key, entryKey),
                                document
                            )
                        )
                        {
                            yield return pair;
                        }
                    }
                }

                break;

            case "arrayobject":
                if (document[key] is IList<object?> list)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        string itemKey = JsonFormsScope.JoinKey(
                            key,
                            i.ToString(CultureInfo.InvariantCulture)
                        );
                        foreach (FieldDefinition child in field.Children)
                        {
                            foreach (
                                (string Key, FieldDefinition Field) pair in Descend(
                                    child,
                                    JsonFormsScope.JoinKey(itemKey, child.Key),
                                    document
                                )
                            )
                            {
                                yield return pair;
                            }
                        }
                    }
                }

                break;

            case "object":
            case "nullable-object":
                foreach (FieldDefinition child in field.Children)
                {
                    foreach (
                        (string Key, FieldDefinition Field) pair in Descend(
                            child,
                            JsonFormsScope.JoinKey(key, child.Key),
                            document
                        )
                    )
                    {
                        yield return pair;
                    }
                }

                break;

            case "oneof":
                if (ActiveVariant(field, key, document) is { } variant)
                {
                    foreach (FieldDefinition child in variant.Children)
                    {
                        foreach (
                            (string Key, FieldDefinition Field) pair in Descend(
                                child,
                                JsonFormsScope.JoinKey(key, child.Key),
                                document
                            )
                        )
                        {
                            yield return pair;
                        }
                    }
                }

                break;

            default:
                break;
        }
    }

    private static OneOfVariant? ActiveVariant(
        FieldDefinition field,
        string key,
        ConfigDocument document
    )
    {
        if (field.DiscriminatorKey is not { Length: > 0 } discriminator)
        {
            return field.OneOfVariants.Count > 0 ? field.OneOfVariants[0] : null;
        }

        string current = document.GetString(JsonFormsScope.JoinKey(key, discriminator));
        return field.OneOfVariants.FirstOrDefault(v =>
                string.Equals(v.DiscriminatorValue, current, StringComparison.Ordinal)
            ) ?? (field.OneOfVariants.Count > 0 ? field.OneOfVariants[0] : null);
    }
}
