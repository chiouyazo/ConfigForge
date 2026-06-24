using System.Text.Json;
using System.Text.Json.Nodes;
using ConfigForge.Core.Internal;
using Json.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConfigForge.Core.Schema;

/// <summary>
/// Parses a combined ConfigForge schema document into a resolved
/// <see cref="ConfigSchema"/> by merging the JSON Schema, the JsonForms UI
/// schema, and the <c>x-cf</c> extension block.
/// </summary>
public sealed partial class JsonFormsSchemaParser : IJsonFormsSchemaParser
{
    private readonly ILogger _logger;

    /// <summary>Creates a parser. The logger is optional.</summary>
    /// <param name="logger">Logger for schema-parse events; null disables logging.</param>
    public JsonFormsSchemaParser(ILogger<JsonFormsSchemaParser>? logger = null) =>
        _logger = (ILogger?)logger ?? NullLogger.Instance;

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Schema parsed {SchemaId} {Version} categories={CategoryCount}"
    )]
    private partial void LogSchemaParsed(string schemaId, string version, int categoryCount);

    private static readonly HashSet<string> ConstraintKeywords = new(StringComparer.Ordinal)
    {
        "type",
        "minimum",
        "maximum",
        "exclusiveMinimum",
        "exclusiveMaximum",
        "minLength",
        "maxLength",
        "pattern",
        "format",
        "enum",
        "const",
        "multipleOf",
        "minItems",
        "maxItems",
        "uniqueItems",
        "items",
    };

    /// <inheritdoc />
    public ConfigSchema Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new SchemaParseException("The combined schema document is not valid JSON.", ex);
        }

        if (root is not JsonObject rootObject)
        {
            throw new SchemaParseException("The combined schema document must be a JSON object.");
        }

        JsonObject schemaObject = GetRequiredObject(rootObject, "schema");
        JsonObject uiObject = GetObjectOrEmpty(rootObject, "uiSchema");
        JsonObject xcf = GetObjectOrEmpty(rootObject, "x-cf");

        ValidateDraft7(schemaObject);

        JsonObject controls = GetObjectOrEmpty(xcf, "controls");
        JsonObject categoriesMeta = GetObjectOrEmpty(xcf, "categories");

        HashSet<string> required = ReadRequired(schemaObject);
        Dictionary<string, FieldDefinition> fields = BuildFields(schemaObject, controls, required);

        List<CategoryElement> categories = BuildCategories(uiObject, categoriesMeta, fields);

        var schema = new ConfigSchema
        {
            Id = GetString(xcf, "id") ?? string.Empty,
            Name = GetString(xcf, "name") ?? string.Empty,
            Version = GetString(xcf, "version") ?? string.Empty,
            PluginId = GetString(xcf, "pluginId"),
            Categories = categories,
            Fields = fields,
            Actions = BuildActions(xcf),
        };

        LogSchemaParsed(schema.Id, schema.Version, schema.Categories.Count);
        return schema;
    }

    private static List<ActionDefinition> BuildActions(JsonObject xcf)
    {
        List<ActionDefinition> actions = [];

        if (xcf["actions"] is not JsonArray actionsArray)
        {
            return actions;
        }

        foreach (JsonNode? node in actionsArray)
        {
            if (node is not JsonObject action)
            {
                continue;
            }

            string? actionId = GetString(action, "actionId");
            if (string.IsNullOrEmpty(actionId))
            {
                continue;
            }

            JsonObject placement = action["placement"] as JsonObject ?? [];

            actions.Add(
                new ActionDefinition
                {
                    ActionId = actionId,
                    Label = GetString(action, "label") ?? actionId,
                    Icon = GetString(action, "icon"),
                    Variant = GetString(action, "variant") ?? "secondary",
                    Category = GetString(placement, "category"),
                    Position = GetString(placement, "position") ?? "bottom",
                }
            );
        }

        return actions;
    }

    /// <inheritdoc />
    public ConfigSchema ParseFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (IOException ex)
        {
            throw new SchemaParseException($"Could not read schema file '{path}'.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new SchemaParseException($"Could not read schema file '{path}'.", ex);
        }

        return Parse(json);
    }

    private static void ValidateDraft7(JsonObject schemaObject)
    {
        try
        {
            _ = JsonSchema.FromText(schemaObject.ToJsonString());
        }
        catch (JsonException ex)
        {
            throw new SchemaParseException(
                "The 'schema' section is not a valid Draft 7 schema.",
                ex
            );
        }
    }

    private static Dictionary<string, FieldDefinition> BuildFields(
        JsonObject schemaObject,
        JsonObject controls,
        HashSet<string> required
    )
    {
        Dictionary<string, FieldDefinition> fields = new(StringComparer.Ordinal);

        if (schemaObject["properties"] is not JsonObject properties)
        {
            return fields;
        }

        foreach (KeyValuePair<string, JsonNode?> property in properties)
        {
            if (property.Value is not JsonObject propSchema)
            {
                continue;
            }

            string key = property.Key;
            JsonObject control = controls[key] as JsonObject ?? [];

            fields[key] = new FieldDefinition
            {
                Key = key,
                ControlType = InferControlType(propSchema, control),
                Title = GetString(propSchema, "title"),
                Description = GetString(propSchema, "description"),
                Tooltip = GetString(control, "tooltip"),
                Placeholder = GetString(control, "placeholder"),
                Unit = GetString(control, "unit"),
                Required = required.Contains(key),
                ReadOnly = GetBool(propSchema, "readOnly") ?? false,
                DefaultValue = propSchema.TryGetPropertyValue("default", out JsonNode? def)
                    ? JsonValueHelper.FromElement(def.Deserialize<JsonElement>())
                    : null,
                LoaderId = GetString(control, "loaderId"),
                ValidatorId = GetString(control, "validatorId"),
                SchemaConstraints = ReadConstraints(propSchema),
                Rules = [],
            };
        }

        return fields;
    }

    /// <summary>
    /// Determines the control type. An explicit <c>x-cf.controls[key].type</c>
    /// always wins. Otherwise inference runs:
    /// boolean → checkbox; string with format date/date-time/time/color →
    /// the matching control; string with enum or a loaderId → select; array of
    /// enum strings → checklist; array of strings → taglist; integer/number →
    /// number, or slider when both minimum and maximum are present; string → text; anything else → text.
    /// </summary>
    private static string InferControlType(JsonObject propSchema, JsonObject control)
    {
        string? explicitType = GetString(control, "type");
        if (!string.IsNullOrEmpty(explicitType))
        {
            return explicitType;
        }

        string? type = GetString(propSchema, "type");
        bool hasEnum = propSchema["enum"] is JsonArray;
        bool hasLoader = !string.IsNullOrEmpty(GetString(control, "loaderId"));

        switch (type)
        {
            case "boolean":
                return "checkbox";

            case "string":
                string? format = GetString(propSchema, "format");
                switch (format)
                {
                    case "date":
                        return "date";
                    case "date-time":
                        return "datetime";
                    case "time":
                        return "time";
                    case "color":
                        return "color";
                    default:
                        break;
                }

                if (hasEnum || hasLoader)
                {
                    return "select";
                }

                return "text";

            case "integer":
            case "number":
                bool hasMin = propSchema.ContainsKey("minimum");
                bool hasMax = propSchema.ContainsKey("maximum");
                return hasMin && hasMax ? "slider" : "number";

            case "array":
                if (propSchema["items"] is JsonObject items)
                {
                    bool itemIsString = GetString(items, "type") == "string";
                    if (items["enum"] is JsonArray && itemIsString)
                    {
                        return "checklist";
                    }

                    if (itemIsString)
                    {
                        return "taglist";
                    }
                }

                return "text";

            default:
                return "text";
        }
    }

    private static Dictionary<string, object> ReadConstraints(JsonObject propSchema)
    {
        Dictionary<string, object> constraints = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, JsonNode?> keyword in propSchema)
        {
            if (!ConstraintKeywords.Contains(keyword.Key) || keyword.Value is null)
            {
                continue;
            }

            object? value = JsonValueHelper.FromElement(keyword.Value.Deserialize<JsonElement>());
            if (value is not null)
            {
                constraints[keyword.Key] = value;
            }
        }

        return constraints;
    }

    private static List<CategoryElement> BuildCategories(
        JsonObject uiObject,
        JsonObject categoriesMeta,
        Dictionary<string, FieldDefinition> fields
    )
    {
        List<CategoryElement> categories = [];
        Dictionary<string, List<JsonFormsRule>> rulesByKey = new(StringComparer.Ordinal);

        if (uiObject.Count == 0)
        {
            categories.Add(
                new CategoryElement
                {
                    Label = "General",
                    Elements =
                    [
                        .. fields.Keys.Select(k => new UiElement
                        {
                            Type = "Control",
                            Scope = $"#/properties/{k}",
                        }),
                    ],
                }
            );
        }
        else if (
            string.Equals(GetString(uiObject, "type"), "Categorization", StringComparison.Ordinal)
        )
        {
            if (uiObject["elements"] is JsonArray categoryNodes)
            {
                foreach (JsonNode? node in categoryNodes)
                {
                    if (node is not JsonObject categoryObject)
                    {
                        continue;
                    }

                    string label = GetString(categoryObject, "label") ?? string.Empty;
                    IReadOnlyList<UiElement> elements = ParseElements(
                        categoryObject["elements"],
                        rulesByKey
                    );
                    categories.Add(DecorateCategory(label, elements, categoriesMeta));
                }
            }
        }
        else
        {
            UiElement rootElement = ParseElement(uiObject, rulesByKey)!;
            categories.Add(DecorateCategory("General", [rootElement], categoriesMeta));
        }

        if (rulesByKey.Count > 0)
        {
            foreach (KeyValuePair<string, List<JsonFormsRule>> entry in rulesByKey)
            {
                if (fields.TryGetValue(entry.Key, out FieldDefinition? field))
                {
                    fields[entry.Key] = CloneWithRules(field, entry.Value);
                }
            }
        }

        return categories;
    }

    private static CategoryElement DecorateCategory(
        string label,
        IReadOnlyList<UiElement> elements,
        JsonObject categoriesMeta
    )
    {
        JsonObject meta = categoriesMeta[label] as JsonObject ?? [];
        return new CategoryElement
        {
            Label = label,
            Icon = GetString(meta, "icon"),
            Description = GetString(meta, "description"),
            Elements = elements,
        };
    }

    private static List<UiElement> ParseElements(
        JsonNode? elementsNode,
        Dictionary<string, List<JsonFormsRule>> rulesByKey
    )
    {
        if (elementsNode is not JsonArray array)
        {
            return [];
        }

        List<UiElement> result = [];
        foreach (JsonNode? node in array)
        {
            UiElement? element = ParseElement(node, rulesByKey);
            if (element is not null)
            {
                result.Add(element);
            }
        }

        return result;
    }

    private static UiElement? ParseElement(
        JsonNode? node,
        Dictionary<string, List<JsonFormsRule>> rulesByKey
    )
    {
        if (node is not JsonObject obj)
        {
            return null;
        }

        string type = GetString(obj, "type") ?? string.Empty;
        string? scope = GetString(obj, "scope");
        JsonFormsRule? rule = ParseRule(obj["rule"]);

        if (rule is not null && scope is not null)
        {
            string? key = ScopeToKey(scope);
            if (key is not null)
            {
                if (!rulesByKey.TryGetValue(key, out List<JsonFormsRule>? list))
                {
                    list = [];
                    rulesByKey[key] = list;
                }

                list.Add(rule);
            }
        }

        return new UiElement
        {
            Type = type,
            Scope = scope,
            Label = GetString(obj, "label"),
            Elements = ParseElements(obj["elements"], rulesByKey),
            Rule = rule,
        };
    }

    private static JsonFormsRule? ParseRule(JsonNode? ruleNode)
    {
        if (ruleNode is not JsonObject ruleObject)
        {
            return null;
        }

        RuleEffect effect = ParseEffect(GetString(ruleObject, "effect"));

        JsonObject? conditionObject = ruleObject["condition"] as JsonObject;
        string conditionScope = conditionObject is null
            ? string.Empty
            : GetString(conditionObject, "scope") ?? string.Empty;
        JsonNode? conditionSchema = conditionObject?["schema"]?.DeepClone();

        return new JsonFormsRule
        {
            Effect = effect,
            Condition = new RuleCondition { Scope = conditionScope, Schema = conditionSchema },
        };
    }

    private static RuleEffect ParseEffect(string? effect) =>
        effect switch
        {
            "HIDE" => RuleEffect.Hide,
            "SHOW" => RuleEffect.Show,
            "DISABLE" => RuleEffect.Disable,
            "ENABLE" => RuleEffect.Enable,
            _ => RuleEffect.None,
        };

    private static FieldDefinition CloneWithRules(
        FieldDefinition field,
        IReadOnlyList<JsonFormsRule> rules
    ) =>
        new()
        {
            Key = field.Key,
            ControlType = field.ControlType,
            Title = field.Title,
            Description = field.Description,
            Tooltip = field.Tooltip,
            Placeholder = field.Placeholder,
            Unit = field.Unit,
            Required = field.Required,
            ReadOnly = field.ReadOnly,
            DefaultValue = field.DefaultValue,
            LoaderId = field.LoaderId,
            ValidatorId = field.ValidatorId,
            SchemaConstraints = field.SchemaConstraints,
            Rules = rules,
        };

    private static HashSet<string> ReadRequired(JsonObject schemaObject)
    {
        HashSet<string> required = new(StringComparer.Ordinal);
        if (schemaObject["required"] is JsonArray array)
        {
            foreach (JsonNode? node in array)
            {
                if (node?.GetValueKind() == JsonValueKind.String)
                {
                    required.Add(node.GetValue<string>());
                }
            }
        }

        return required;
    }

    private static string? ScopeToKey(string scope)
    {
        const string prefix = "#/properties/";
        return scope.StartsWith(prefix, StringComparison.Ordinal) ? scope[prefix.Length..] : null;
    }

    private static JsonObject GetRequiredObject(JsonObject parent, string name)
    {
        if (parent[name] is JsonObject obj)
        {
            return obj;
        }

        throw new SchemaParseException(
            $"The combined schema document is missing the required '{name}' object."
        );
    }

    private static JsonObject GetObjectOrEmpty(JsonObject parent, string name) =>
        parent[name] as JsonObject ?? [];

    private static string? GetString(JsonObject obj, string name) =>
        obj[name]?.GetValueKind() == JsonValueKind.String ? obj[name]!.GetValue<string>() : null;

    private static bool? GetBool(JsonObject obj, string name)
    {
        JsonValueKind kind = obj[name]?.GetValueKind() ?? JsonValueKind.Undefined;
        return kind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }
}
