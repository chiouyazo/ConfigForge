using System.Text.Json;
using System.Text.Json.Nodes;
using ConfigForge.Abstractions;
using ConfigForge.Core.Internal;
using ConfigForge.Core.Schema;
using Json.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConfigForge.Core.Documents;

/// <summary>
/// Parses, serializes, and diffs configuration documents. <see cref="Parse"/>
/// never throws: any failure is reported on the returned result.
/// </summary>
public sealed partial class ConfigDocumentEngine : IConfigDocumentEngine
{
    private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };

    private readonly ILogger _logger;

    /// <summary>Creates an engine. The logger is optional.</summary>
    /// <param name="logger">Logger for config-load events; null disables logging.</param>
    public ConfigDocumentEngine(ILogger<ConfigDocumentEngine>? logger = null) =>
        _logger = (ILogger?)logger ?? NullLogger.Instance;

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Config loaded {SchemaId} valid={IsValid} unknownKeys={UnknownCount} missingRequired={MissingCount}"
    )]
    private partial void LogConfigLoaded(
        string schemaId,
        bool isValid,
        int unknownCount,
        int missingCount
    );

    /// <inheritdoc />
    public ConfigDocumentParseResult Parse(string json, ConfigSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        JsonObject rootObject;
        try
        {
            if (JsonNode.Parse(json) is not JsonObject parsed)
            {
                return new ConfigDocumentParseResult
                {
                    Document = new ConfigDocument(),
                    JsonError = "The configuration JSON must be a JSON object.",
                };
            }

            rootObject = parsed;
        }
        catch (JsonException ex)
        {
            return new ConfigDocumentParseResult
            {
                Document = new ConfigDocument(),
                JsonError = ex.Message,
            };
        }
        catch (ArgumentNullException ex)
        {
            return new ConfigDocumentParseResult
            {
                Document = new ConfigDocument(),
                JsonError = ex.Message,
            };
        }

        ConfigDocument document = new();
        foreach (KeyValuePair<string, JsonNode?> member in rootObject)
        {
            document[member.Key] = member.Value is null
                ? null
                : JsonValueHelper.FromElement(member.Value.Deserialize<JsonElement>());
        }

        List<string> unknownKeys = [.. document.Keys.Where(k => !schema.Fields.ContainsKey(k))];

        List<string> missingRequired =
        [
            .. schema
                .Fields.Values.Where(f => f.Required && !document.ContainsKey(f.Key))
                .Select(f => f.Key),
        ];

        List<ValidationError> invalidValues = ValidateValues(document, schema);

        var result = new ConfigDocumentParseResult
        {
            Document = document,
            UnknownKeys = unknownKeys,
            MissingRequiredKeys = missingRequired,
            InvalidValues = invalidValues,
        };

        LogConfigLoaded(schema.Id, result.IsValid, unknownKeys.Count, missingRequired.Count);
        return result;
    }

    /// <inheritdoc />
    public string Serialize(ConfigDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        JsonObject root = [];
        foreach (string key in document.Keys)
        {
            root[key] = JsonValueHelper.ToJsonNode(document[key]);
        }

        return root.ToJsonString(IndentedOptions);
    }

    /// <inheritdoc />
    public ConfigDiff Diff(ConfigDocument original, ConfigDocument modified)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(modified);

        List<ConfigChange> changes = [];

        HashSet<string> allKeys = new(original.Keys, StringComparer.Ordinal);
        allKeys.UnionWith(modified.Keys);

        foreach (string key in allKeys)
        {
            bool inOriginal = original.TryGetValue(key, out object? oldValue);
            bool inModified = modified.TryGetValue(key, out object? newValue);

            if (inOriginal && !inModified)
            {
                changes.Add(
                    new ConfigChange
                    {
                        Key = key,
                        Kind = ConfigChangeKind.Removed,
                        OldValue = oldValue,
                    }
                );
            }
            else if (!inOriginal && inModified)
            {
                changes.Add(
                    new ConfigChange
                    {
                        Key = key,
                        Kind = ConfigChangeKind.Added,
                        NewValue = newValue,
                    }
                );
            }
            else if (
                !string.Equals(
                    JsonValueHelper.Serialize(oldValue),
                    JsonValueHelper.Serialize(newValue),
                    StringComparison.Ordinal
                )
            )
            {
                changes.Add(
                    new ConfigChange
                    {
                        Key = key,
                        Kind = ConfigChangeKind.Modified,
                        OldValue = oldValue,
                        NewValue = newValue,
                    }
                );
            }
        }

        return new ConfigDiff { Changes = changes };
    }

    private static List<ValidationError> ValidateValues(
        ConfigDocument document,
        ConfigSchema schema
    )
    {
        List<ValidationError> errors = [];

        foreach (KeyValuePair<string, FieldDefinition> entry in schema.Fields)
        {
            if (!document.TryGetValue(entry.Key, out object? value))
            {
                continue;
            }

            JsonSchema? fieldSchema = FieldSchemaBuilder.Build(entry.Value);
            if (fieldSchema is null)
            {
                continue;
            }

            JsonNode? valueNode = JsonValueHelper.ToJsonNode(value);
            EvaluationResults results = fieldSchema.Evaluate(valueNode, FieldSchemaBuilder.Options);
            if (!results.IsValid)
            {
                errors.Add(
                    new ValidationError { Key = entry.Key, Message = DescribeFailure(results) }
                );
            }
        }

        return errors;
    }

    private static string DescribeFailure(EvaluationResults results)
    {
        List<string> messages = [];
        CollectErrors(results, messages);
        return messages.Count > 0
            ? string.Join("; ", messages)
            : "Value does not satisfy the field schema.";
    }

    private static void CollectErrors(EvaluationResults results, List<string> messages)
    {
        if (results.HasErrors && results.Errors is not null)
        {
            foreach (KeyValuePair<string, string> error in results.Errors)
            {
                messages.Add(error.Value);
            }
        }

        foreach (EvaluationResults detail in results.Details)
        {
            CollectErrors(detail, messages);
        }
    }
}
