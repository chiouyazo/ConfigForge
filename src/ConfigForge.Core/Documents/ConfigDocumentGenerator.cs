using System.Globalization;
using System.Text.RegularExpressions;
using ConfigForge.Abstractions;
using ConfigForge.Core.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConfigForge.Core.Documents;

/// <summary>
/// Generates seed documents from a resolved schema: a fully populated example or
/// a defaults-only document.
/// </summary>
public sealed partial class ConfigDocumentGenerator : IConfigDocumentGenerator
{
    private readonly ILogger _logger;

    /// <summary>Creates a generator. The logger is optional.</summary>
    /// <param name="logger">Logger for generation events; null disables logging.</param>
    public ConfigDocumentGenerator(ILogger<ConfigDocumentGenerator>? logger = null) =>
        _logger = (ILogger?)logger ?? NullLogger.Instance;

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Document generated mode={Mode} schemaId={SchemaId}"
    )]
    private partial void LogDocumentGenerated(string mode, string schemaId);

    /// <inheritdoc />
    public ConfigDocument GenerateExample(ConfigSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        ConfigDocument document = new();
        foreach (KeyValuePair<string, FieldDefinition> entry in schema.Fields)
        {
            document[entry.Key] = ExampleForField(entry.Value);
        }

        LogDocumentGenerated("Example", schema.Id);
        return document;
    }

    /// <inheritdoc />
    public ConfigDocument GenerateEmpty(ConfigSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        ConfigDocument document = new();
        foreach (
            KeyValuePair<string, FieldDefinition> entry in schema.Fields.Where(e =>
                e.Value.DefaultValue is not null
            )
        )
        {
            document[entry.Key] = entry.Value.DefaultValue;
        }

        LogDocumentGenerated("Empty", schema.Id);
        return document;
    }

    private static object? ExampleForField(FieldDefinition field)
    {
        switch (field.ControlType)
        {
            case "arrayobject":
                return new List<object?> { BuildObjectExample(field.Children) };
            case "map":
                return new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["example"] = field.ValueField is null
                        ? "example"
                        : ExampleForField(field.ValueField),
                };
            case "object":
                return BuildObjectExample(field.Children);
            default:
                return ExampleValue(field);
        }
    }

    private static Dictionary<string, object?> BuildObjectExample(
        IReadOnlyList<FieldDefinition> children
    )
    {
        Dictionary<string, object?> item = new(StringComparer.Ordinal);
        foreach (FieldDefinition child in children)
        {
            AssignPath(item, child.Key, ExampleForField(child));
        }

        return item;
    }

    private static void AssignPath(Dictionary<string, object?> target, string path, object? value)
    {
        string[] segments = path.Split('/');
        Dictionary<string, object?> node = target;
        for (int i = 0; i < segments.Length - 1; i++)
        {
            if (
                !node.TryGetValue(segments[i], out object? child)
                || child is not Dictionary<string, object?> childDict
            )
            {
                childDict = new Dictionary<string, object?>(StringComparer.Ordinal);
                node[segments[i]] = childDict;
            }

            node = childDict;
        }

        node[segments[^1]] = value;
    }

    private static object? ExampleValue(FieldDefinition field)
    {
        if (field.DefaultValue is not null)
        {
            return field.DefaultValue;
        }

        if (TryFirstEnumValue(field, out object? enumValue))
        {
            return enumValue;
        }

        string type = ConstraintString(field, "type") ?? "string";
        return type switch
        {
            "boolean" => true,
            "integer" => MinimumOr(field, 0L),
            "number" => MinimumOrDouble(field, 0.0),
            "array" => new List<object?> { ExampleArrayItem(field) },
            "string" => ExampleString(field),
            _ => ExampleString(field),
        };
    }

    private static readonly string[] PatternCandidates =
    [
        "https://example.com",
        "http://example.com",
        "example@example.com",
        "example.com",
        "Example1",
        "example123",
        "+12025550123",
        "1234567890",
        "00000000-0000-0000-0000-000000000000",
        "192.168.0.1",
        "2020-01-01",
        "example",
        "a",
        "1",
    ];

    private static string ExampleString(FieldDefinition field)
    {
        string? format = ConstraintString(field, "format");
        string formatted = format switch
        {
            "date" => DateTime.UtcNow.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "date-time" => DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            "time" => DateTime.UtcNow.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            "color" => "#000000",
            _ => "example",
        };

        string? pattern = ConstraintString(field, "pattern");
        if (string.IsNullOrEmpty(pattern) || MatchesPattern(formatted, pattern))
        {
            return formatted;
        }

        return Array.Find(PatternCandidates, candidate => MatchesPattern(candidate, pattern))
            ?? formatted;
    }

    private static bool MatchesPattern(string value, string pattern)
    {
        try
        {
            return Regex.IsMatch(value, pattern, RegexOptions.None, TimeSpan.FromMilliseconds(100));
        }
        catch (ArgumentException)
        {
            return true;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static object ExampleArrayItem(FieldDefinition field)
    {
        if (
            field.SchemaConstraints.TryGetValue("items", out object? items)
            && items is IReadOnlyDictionary<string, object?> itemMap
            && itemMap.TryGetValue("type", out object? itemType)
            && itemType is string itemTypeName
        )
        {
            return itemTypeName switch
            {
                "integer" => 0L,
                "number" => 0.0,
                "boolean" => true,
                _ => "example",
            };
        }

        return "example";
    }

    private static bool TryFirstEnumValue(FieldDefinition field, out object? value)
    {
        if (
            field.SchemaConstraints.TryGetValue("enum", out object? raw)
            && raw is System.Collections.IEnumerable enumerable
            && raw is not string
        )
        {
            System.Collections.IEnumerator enumerator = enumerable.GetEnumerator();
            if (enumerator.MoveNext())
            {
                value = enumerator.Current;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static long MinimumOr(FieldDefinition field, long fallback)
    {
        if (field.SchemaConstraints.TryGetValue("minimum", out object? min))
        {
            return min switch
            {
                long l => l,
                double d => (long)d,
                int i => (long)i,
                _ => fallback,
            };
        }

        return fallback;
    }

    private static double MinimumOrDouble(FieldDefinition field, double fallback)
    {
        if (field.SchemaConstraints.TryGetValue("minimum", out object? min))
        {
            return min switch
            {
                double d => d,
                long l => (double)l,
                int i => (double)i,
                _ => fallback,
            };
        }

        return fallback;
    }

    private static string? ConstraintString(FieldDefinition field, string key) =>
        field.SchemaConstraints.TryGetValue(key, out object? value) ? value as string : null;
}
