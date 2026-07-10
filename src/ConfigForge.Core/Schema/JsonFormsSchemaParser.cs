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

        Dictionary<string, FieldDefinition> fields = BuildFields(schemaObject, controls);

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
            UntrackedKeys = new HashSet<string>(
                fields.Values.Where(f => !f.Tracked).Select(f => f.Key),
                StringComparer.Ordinal
            ),
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
        JsonObject controls
    )
    {
        Dictionary<string, FieldDefinition> fields = new(StringComparer.Ordinal);
        foreach (
            FieldDefinition field in BuildObjectFields(
                schemaObject,
                schemaObject,
                controls,
                string.Empty
            )
        )
        {
            fields[field.Key] = field;
        }

        return fields;
    }

    /// <summary>
    /// Builds the field list for one object schema in declaration order. Plain
    /// nested objects are flattened into path-keyed leaves; arrays of objects and
    /// <c>additionalProperties</c> maps become single composite fields that carry
    /// their child or value templates.
    /// </summary>
    private static List<FieldDefinition> BuildObjectFields(
        JsonObject objectSchema,
        JsonObject rootSchema,
        JsonObject controls,
        string prefix,
        string? inheritedSection = null
    )
    {
        List<FieldDefinition> fields = [];
        if (objectSchema["properties"] is not JsonObject properties)
        {
            return fields;
        }

        HashSet<string> required = ReadRequired(objectSchema);

        foreach (KeyValuePair<string, JsonNode?> property in properties)
        {
            if (property.Value is not JsonObject rawProp)
            {
                continue;
            }

            JsonObject propSchema = ResolveRef(rawProp, rootSchema);
            string key = prefix.Length == 0 ? property.Key : $"{prefix}/{property.Key}";
            JsonObject control = controls[key] as JsonObject ?? [];

            bool hasExplicitControl =
                !string.IsNullOrEmpty(GetString(control, "type"))
                || !string.IsNullOrEmpty(GetString(propSchema, "x-control"));
            if (!hasExplicitControl && IsPlainObject(propSchema))
            {
                // Flattened nested objects: children inherit the parent's section so a
                // sectioned object property still lands in the right tab/group.
                string? childSection = GetString(propSchema, "x-section") ?? inheritedSection;
                fields.AddRange(
                    BuildObjectFields(propSchema, rootSchema, controls, key, childSection)
                );
                continue;
            }

            fields.Add(
                BuildField(
                    key,
                    propSchema,
                    control,
                    rootSchema,
                    required.Contains(property.Key),
                    inheritedSection
                )
            );
        }

        return fields;
    }

    private static FieldDefinition BuildField(
        string key,
        JsonObject propSchema,
        JsonObject control,
        JsonObject rootSchema,
        bool required,
        string? inheritedSection = null
    )
    {
        string controlType = InferControlType(propSchema, control, rootSchema);

        IReadOnlyList<FieldDefinition> children = [];
        FieldDefinition? valueField = null;
        string? discriminatorKey = null;
        IReadOnlyList<OneOfVariant> oneOfVariants = [];

        if (
            string.Equals(controlType, "arrayobject", StringComparison.Ordinal)
            && ResolveRef(propSchema["items"] as JsonObject, rootSchema) is JsonObject itemSchema
        )
        {
            children = BuildObjectFields(itemSchema, rootSchema, [], string.Empty);
        }
        else if (
            string.Equals(controlType, "map", StringComparison.Ordinal)
            && ResolveRef(propSchema["additionalProperties"] as JsonObject, rootSchema)
                is JsonObject valueSchema
        )
        {
            valueField = BuildValueField(valueSchema, rootSchema);
        }
        else if (
            string.Equals(controlType, "oneof", StringComparison.Ordinal)
            && propSchema["oneOf"] is JsonArray variantNodes
        )
        {
            (discriminatorKey, oneOfVariants) = BuildOneOfVariants(variantNodes, rootSchema);
        }

        return new FieldDefinition
        {
            Key = key,
            ControlType = controlType,
            Title = GetString(propSchema, "title"),
            Description = GetString(propSchema, "description"),
            Tooltip = GetString(control, "tooltip") ?? GetString(propSchema, "x-tooltip"),
            Placeholder =
                GetString(control, "placeholder") ?? GetString(propSchema, "x-placeholder"),
            Unit = GetString(control, "unit") ?? GetString(propSchema, "x-unit"),
            Required = required,
            ReadOnly = GetBool(propSchema, "readOnly") ?? false,
            DefaultValue = propSchema.TryGetPropertyValue("default", out JsonNode? def)
                ? JsonValueHelper.FromElement(def.Deserialize<JsonElement>())
                : null,
            LoaderId = GetString(control, "loaderId") ?? GetString(propSchema, "x-loader"),
            ValidatorId = GetString(control, "validatorId") ?? GetString(propSchema, "x-validator"),
            SchemaConstraints = ReadConstraints(propSchema),
            Rules = ParseInlineRules(propSchema),
            Tracked = GetBool(control, "tracked") ?? GetBool(propSchema, "x-tracked") ?? true,
            Children = children,
            ValueField = valueField,
            KeyFormat = GetString(control, "keyFormat") ?? GetString(propSchema, "x-key-format"),
            Section =
                GetString(control, "section")
                ?? GetString(propSchema, "x-section")
                ?? inheritedSection,
            DiscriminatorKey = discriminatorKey,
            OneOfVariants = oneOfVariants,
        };
    }

    /// <summary>
    /// Parses a schema <c>oneOf</c> into selectable variants. The discriminator is the
    /// property that carries a <c>const</c> in each variant (e.g. <c>Type</c>); its
    /// const value selects the variant and is excluded from the variant's fields.
    /// </summary>
    private static (
        string? DiscriminatorKey,
        IReadOnlyList<OneOfVariant> Variants
    ) BuildOneOfVariants(JsonArray variantNodes, JsonObject rootSchema)
    {
        string? discriminatorKey = null;
        List<OneOfVariant> variants = [];

        foreach (JsonNode? node in variantNodes)
        {
            if (ResolveRef(node as JsonObject, rootSchema) is not { } variantSchema)
            {
                continue;
            }

            if (variantSchema["properties"] is not JsonObject properties)
            {
                continue;
            }

            (string key, string value)? discriminator = FindDiscriminator(properties);
            if (discriminator is null)
            {
                continue;
            }

            discriminatorKey ??= discriminator.Value.key;

            List<FieldDefinition> children =
            [
                .. BuildObjectFields(variantSchema, rootSchema, [], string.Empty)
                    .Where(f =>
                        !string.Equals(f.Key, discriminator.Value.key, StringComparison.Ordinal)
                    ),
            ];

            variants.Add(
                new OneOfVariant
                {
                    DiscriminatorValue = discriminator.Value.value,
                    Label = discriminator.Value.value,
                    Children = children,
                }
            );
        }

        return (discriminatorKey, variants);
    }

    private static (string key, string value)? FindDiscriminator(JsonObject properties)
    {
        foreach (KeyValuePair<string, JsonNode?> property in properties)
        {
            if (
                property.Value is JsonObject prop
                && prop["const"]?.GetValueKind() == JsonValueKind.String
            )
            {
                return (property.Key, prop["const"]!.GetValue<string>());
            }
        }

        return null;
    }

    /// <summary>Builds the per-entry value template for a <c>map</c> control.</summary>
    private static FieldDefinition BuildValueField(JsonObject valueSchema, JsonObject rootSchema)
    {
        if (IsPlainObject(valueSchema))
        {
            return new FieldDefinition
            {
                Key = string.Empty,
                ControlType = "object",
                Children = BuildObjectFields(valueSchema, rootSchema, [], string.Empty),
            };
        }

        return BuildField(string.Empty, valueSchema, [], rootSchema, required: false);
    }

    private static bool IsPlainObject(JsonObject schema)
    {
        string? type = GetString(schema, "type");
        return (type is null || string.Equals(type, "object", StringComparison.Ordinal))
            && schema["properties"] is JsonObject;
    }

    /// <summary>
    /// Resolves a local <c>$ref</c> (<c>#/definitions/…</c> or <c>#/$defs/…</c>) to
    /// its target, following chains and stopping on cycles. Returns the node
    /// unchanged when it has no <c>$ref</c> or the target cannot be found.
    /// </summary>
    private static JsonObject ResolveRef(JsonObject? node, JsonObject rootSchema)
    {
        JsonObject? current = node;
        HashSet<string> seen = new(StringComparer.Ordinal);

        while (current is not null && GetString(current, "$ref") is { Length: > 0 } pointer)
        {
            if (!seen.Add(pointer))
            {
                break;
            }

            JsonObject? target = ResolvePointer(rootSchema, pointer);
            if (target is null)
            {
                break;
            }

            current = target;
        }

        return current ?? [];
    }

    private static JsonObject? ResolvePointer(JsonObject root, string pointer)
    {
        if (!pointer.StartsWith("#/", StringComparison.Ordinal))
        {
            return null;
        }

        JsonNode? node = root;
        foreach (string rawSegment in pointer[2..].Split('/'))
        {
            string segment = rawSegment
                .Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal);

            if (node is JsonObject obj && obj.TryGetPropertyValue(segment, out JsonNode? child))
            {
                node = child;
            }
            else
            {
                return null;
            }
        }

        return node as JsonObject;
    }

    /// <summary>
    /// Determines the control type. An explicit <c>x-cf.controls[key].type</c>
    /// always wins, then an inline <c>x-control</c> keyword on the property schema
    /// (which, unlike the controls map, is carried into oneof variants, array items,
    /// and map values). Otherwise inference runs:
    /// boolean → checkbox; string with format date/date-time/time/color →
    /// the matching control; string with enum or a loaderId → select; array of
    /// enum strings → checklist; array of strings → taglist; array of objects →
    /// arrayobject; integer/number → number, or slider when both minimum and maximum
    /// are present; object with additionalProperties → map; string → text;
    /// anything else → text.
    /// </summary>
    private static string InferControlType(
        JsonObject propSchema,
        JsonObject control,
        JsonObject rootSchema
    )
    {
        string? explicitType = GetString(control, "type") ?? GetString(propSchema, "x-control");
        if (!string.IsNullOrEmpty(explicitType))
        {
            return explicitType;
        }

        if (propSchema["oneOf"] is JsonArray)
        {
            return "oneof";
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
                JsonObject? items = ResolveRef(propSchema["items"] as JsonObject, rootSchema);
                if (items is not null && IsObjectSchema(items))
                {
                    return "arrayobject";
                }

                if (items is not null)
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

            case "object":
                return propSchema["additionalProperties"] is JsonObject ? "map" : "text";

            default:
                return propSchema["additionalProperties"] is JsonObject ? "map" : "text";
        }
    }

    private static bool IsObjectSchema(JsonObject schema)
    {
        string? type = GetString(schema, "type");
        return string.Equals(type, "object", StringComparison.Ordinal)
            || (type is null && schema["properties"] is JsonObject);
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
                            Scope = JsonFormsScope.ToScope(k),
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
            CollectionKey = GetString(meta, "collection"),
            CollectionEntryLabelKey = GetString(meta, "collectionLabel"),
            CollectionAddLabel = GetString(meta, "collectionAddLabel"),
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
            string? key = JsonFormsScope.ToKey(scope);
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

    // Combines any inline (x-rule) rules already on the field with the rules discovered on the
    // uiSchema layout, preserving every other member (WithRules is a faithful copy).
    private static FieldDefinition CloneWithRules(
        FieldDefinition field,
        IReadOnlyList<JsonFormsRule> rules
    ) => field.WithRules([.. field.Rules, .. rules]);

    /// <summary>Reads an inline <c>x-rule</c> (a single rule object or an array of them) off a property.</summary>
    private static List<JsonFormsRule> ParseInlineRules(JsonObject propSchema)
    {
        JsonNode? node = propSchema["x-rule"];
        if (node is JsonArray array)
        {
            List<JsonFormsRule> parsed = [];
            foreach (JsonNode? element in array)
            {
                if (ParseRule(element) is { } rule)
                {
                    parsed.Add(rule);
                }
            }

            return parsed;
        }

        return ParseRule(node) is { } single ? [single] : [];
    }

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
