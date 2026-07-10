using System.Collections;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ConfigForge.Abstractions.Annotations;
using ConfigForge.Core.Schema;

namespace ConfigForge.Core.Schema.Generation;

/// <summary>
/// Reflection-based implementation of <see cref="IClrSchemaGenerator"/>. It walks a
/// CLR type graph and emits standard draft-07 JSON Schema plus inline <c>x-*</c>
/// control hints, wrapped in the combined ConfigForge document.
/// </summary>
public sealed class ClrSchemaGenerator : IClrSchemaGenerator
{
    /// <inheritdoc />
    public string Generate<T>(SchemaGenerationOptions options) => Generate(typeof(T), options);

    /// <inheritdoc />
    public string Generate(Type rootType, SchemaGenerationOptions options)
    {
        ArgumentNullException.ThrowIfNull(rootType);
        ArgumentNullException.ThrowIfNull(options);

        HashSet<Type> visited = [];
        JsonObject schema = BuildObjectSchema(rootType, options, visited, depth: 0);

        JsonObject xcf = new()
        {
            ["id"] = options.Id,
            ["name"] = options.Name,
            ["version"] = options.Version,
        };

        EmitCategoryMeta(rootType, xcf);
        EmitActions(rootType, xcf);

        JsonObject document = new() { ["schema"] = schema, ["x-cf"] = xcf };

        JsonObject? uiSchema = BuildCategorization(rootType, schema, options);
        if (uiSchema is not null)
        {
            document["uiSchema"] = uiSchema;
        }

        if (options.Overlay is JsonObject overlay)
        {
            DeepMerge(document, overlay);
        }

        return document.ToJsonString();
    }

    private static JsonObject BuildTypeSchema(
        Type type,
        SchemaGenerationOptions options,
        HashSet<Type> visited,
        int depth
    )
    {
        Type actual = Nullable.GetUnderlyingType(type) ?? type;

        if (depth >= options.MaxDepth)
        {
            return new JsonObject { ["type"] = "string" };
        }

        if (IsFreeformJson(actual))
        {
            return new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = true,
                ["x-control"] = "code",
            };
        }

        if (TryBuildScalarSchema(actual, options, out JsonObject? scalar))
        {
            return scalar;
        }

        if (TryGetDictionaryTypes(actual, out Type? keyType, out Type? valueType))
        {
            JsonObject map = new()
            {
                ["type"] = "object",
                ["additionalProperties"] = BuildTypeSchema(valueType, options, visited, depth + 1),
            };
            if (keyType == typeof(Guid))
            {
                // GUID-keyed dictionaries have no meaningful user-facing key: the editor
                // hides the key field and generates one on add.
                map["x-key-format"] = "uuid";
            }

            return map;
        }

        if (TryGetEnumerableElementType(actual, out Type? elementType))
        {
            return new JsonObject
            {
                ["type"] = "array",
                ["items"] = BuildTypeSchema(elementType, options, visited, depth + 1),
            };
        }

        if (HasPolymorphism(actual))
        {
            return BuildOneOfSchema(actual, options, visited, depth);
        }

        return BuildObjectSchema(actual, options, visited, depth);
    }

    private static bool TryBuildScalarSchema(
        Type type,
        SchemaGenerationOptions options,
        out JsonObject schema
    )
    {
        if (type == typeof(string))
        {
            schema = new JsonObject { ["type"] = "string" };
            return true;
        }

        if (type == typeof(bool))
        {
            schema = new JsonObject { ["type"] = "boolean" };
            return true;
        }

        if (IsIntegerType(type))
        {
            schema = new JsonObject { ["type"] = "integer" };
            return true;
        }

        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
        {
            schema = new JsonObject { ["type"] = "number" };
            return true;
        }

        if (type.IsEnum)
        {
            schema = BuildEnumSchema(type, options);
            return true;
        }

        if (TryBuildStringFormatSchema(type, out schema!))
        {
            return true;
        }

        schema = new JsonObject();
        return false;
    }

    private static bool TryBuildStringFormatSchema(Type type, out JsonObject? schema)
    {
        string? format = type switch
        {
            _ when type == typeof(DateTime) || type == typeof(DateTimeOffset) => "date-time",
            _ when type == typeof(DateOnly) => "date",
            _ when type == typeof(TimeOnly) || type == typeof(TimeSpan) => "time",
            _ when type == typeof(Guid) => "uuid",
            _ when type == typeof(Uri) => "uri",
            _ => null,
        };

        if (format is null)
        {
            schema = null;
            return false;
        }

        schema = new JsonObject { ["type"] = "string", ["format"] = format };
        return true;
    }

    private static JsonObject BuildEnumSchema(Type enumType, SchemaGenerationOptions options)
    {
        JsonArray values = [];
        foreach (string name in Enum.GetNames(enumType))
        {
            values.Add(options.EnumNamingPolicy.ConvertName(name));
        }

        return new JsonObject { ["type"] = "string", ["enum"] = values };
    }

    private static JsonObject BuildObjectSchema(
        Type type,
        SchemaGenerationOptions options,
        HashSet<Type> visited,
        int depth
    )
    {
        if (!visited.Add(type))
        {
            // Cycle: stop descending and leave an open object.
            return new JsonObject { ["type"] = "object" };
        }

        try
        {
            JsonObject properties = [];
            JsonArray required = [];

            foreach (PropertyInfo property in GetSerializableProperties(type, options))
            {
                string name = ResolvePropertyName(property, options);
                properties[name] = BuildPropertySchema(property, options, visited, depth + 1);
                if (IsRequired(property))
                {
                    required.Add(name);
                }
            }

            JsonObject schema = new() { ["type"] = "object", ["properties"] = properties };
            if (required.Count > 0)
            {
                schema["required"] = required;
            }

            return schema;
        }
        finally
        {
            visited.Remove(type);
        }
    }

    private static JsonObject BuildOneOfSchema(
        Type baseType,
        SchemaGenerationOptions options,
        HashSet<Type> visited,
        int depth
    )
    {
        string discriminator =
            baseType.GetCustomAttribute<JsonPolymorphicAttribute>()?.TypeDiscriminatorPropertyName
            ?? "$type";

        JsonArray variants = [];
        foreach (JsonDerivedTypeAttribute derived in GetDerivedTypes(baseType))
        {
            JsonObject variant = BuildObjectSchema(
                derived.DerivedType,
                options,
                visited,
                depth + 1
            );
            JsonObject properties = variant["properties"] as JsonObject ?? [];

            // Rebuild properties with the discriminator const first so it is the field
            // the parser treats as the variant selector.
            JsonObject ordered = new()
            {
                [discriminator] = new JsonObject
                {
                    ["type"] = "string",
                    ["const"] = DiscriminatorValue(derived),
                },
            };
            foreach (KeyValuePair<string, JsonNode?> entry in properties)
            {
                ordered[entry.Key] = entry.Value?.DeepClone();
            }

            variant["properties"] = ordered;
            variants.Add(variant);
        }

        return new JsonObject { ["oneOf"] = variants };
    }

    private static JsonObject BuildPropertySchema(
        PropertyInfo property,
        SchemaGenerationOptions options,
        HashSet<Type> visited,
        int depth
    )
    {
        string? controlOverride = ResolveControlOverride(property, options);

        JsonObject schema = IsSecret(property, options)
            ? new JsonObject { ["type"] = "string" }
            : BuildTypeSchema(property.PropertyType, options, visited, depth);

        schema["title"] = ResolveTitle(property);

        string? description = ResolveDescription(property);
        if (description is not null)
        {
            schema["description"] = description;
        }

        if (controlOverride is not null)
        {
            schema["x-control"] = controlOverride;
        }

        CfOptionsAttribute? options2 = Options(property);
        ApplyStringHint(
            schema,
            "x-tooltip",
            property.GetCustomAttribute<CfTooltipAttribute>()?.Tooltip ?? options2?.Tooltip
        );
        ApplyStringHint(
            schema,
            "x-placeholder",
            property.GetCustomAttribute<CfPlaceholderAttribute>()?.Placeholder
                ?? options2?.Placeholder
        );
        ApplyStringHint(
            schema,
            "x-unit",
            property.GetCustomAttribute<CfUnitAttribute>()?.Unit ?? options2?.Unit
        );
        // Inline so composite controls (oneof variants) can tab-group their children.
        ApplyStringHint(
            schema,
            "x-section",
            property.GetCustomAttribute<CfSectionAttribute>()?.Section ?? options2?.Section
        );
        ApplyStringHint(
            schema,
            "x-loader",
            property.GetCustomAttribute<CfLoaderAttribute>()?.LoaderId ?? options2?.Loader
        );

        if (
            property.GetCustomAttribute<CfUntrackedAttribute>() is not null
            || options2?.Tracked == false
        )
        {
            schema["x-tracked"] = false;
        }

        if (options2?.ReadOnly == true)
        {
            schema["readOnly"] = true;
        }

        List<CfRuleAttribute> rules = [.. property.GetCustomAttributes<CfRuleAttribute>()];
        if (rules.Count == 1)
        {
            schema["x-rule"] = BuildRule(rules[0]);
        }
        else if (rules.Count > 1)
        {
            schema["x-rule"] = new JsonArray([.. rules.Select(r => (JsonNode)BuildRule(r))]);
        }

        AddValidationConstraints(schema, property);
        return schema;
    }

    /// <summary>The consolidated <c>[CfOptions]</c> on a property, if present.</summary>
    private static CfOptionsAttribute? Options(PropertyInfo property) =>
        property.GetCustomAttribute<CfOptionsAttribute>();

    /// <summary>Emits <c>x-cf.categories[label]</c> icon/description from <c>[CfCategoryMeta]</c>.</summary>
    private static void EmitCategoryMeta(Type rootType, JsonObject xcf)
    {
        List<CfCategoryMetaAttribute> metas =
        [
            .. rootType.GetCustomAttributes<CfCategoryMetaAttribute>(),
        ];
        if (metas.Count == 0)
        {
            return;
        }

        JsonObject categories = xcf["categories"] as JsonObject ?? [];
        foreach (CfCategoryMetaAttribute meta in metas)
        {
            JsonObject entry = categories[meta.Category] as JsonObject ?? [];
            if (meta.Icon is not null)
            {
                entry["icon"] = meta.Icon;
            }

            if (meta.Description is not null)
            {
                entry["description"] = meta.Description;
            }

            categories[meta.Category] = entry;
        }

        xcf["categories"] = categories;
    }

    /// <summary>Emits <c>x-cf.actions</c> from <c>[CfAction]</c> declarations on the type.</summary>
    private static void EmitActions(Type rootType, JsonObject xcf)
    {
        List<CfActionAttribute> actions = [.. rootType.GetCustomAttributes<CfActionAttribute>()];
        if (actions.Count == 0)
        {
            return;
        }

        JsonArray array = [];
        foreach (CfActionAttribute action in actions)
        {
            JsonObject placement = new() { ["position"] = action.Position };
            if (action.Category is not null)
            {
                placement["category"] = action.Category;
            }

            JsonObject entry = new()
            {
                ["actionId"] = action.ActionId,
                ["variant"] = action.Variant,
                ["placement"] = placement,
            };
            if (action.Label is not null)
            {
                entry["label"] = action.Label;
            }

            if (action.Icon is not null)
            {
                entry["icon"] = action.Icon;
            }

            array.Add(entry);
        }

        xcf["actions"] = array;
    }

    /// <summary>Builds an inline JsonForms rule (<c>x-rule</c>) from a <c>[CfEnableWhen]</c>/<c>[CfVisibleWhen]</c>.</summary>
    private static JsonObject BuildRule(CfRuleAttribute rule) =>
        new()
        {
            ["effect"] = rule.Effect,
            ["condition"] = new JsonObject
            {
                ["scope"] = ScopeFromPath(rule.FieldPath),
                ["schema"] = new JsonObject
                {
                    ["const"] = JsonSerializer.SerializeToNode(rule.EqualsValue),
                },
            },
        };

    /// <summary>Converts a slash path (<c>a/b</c>) to a JsonForms scope (<c>#/properties/a/properties/b</c>).</summary>
    private static string ScopeFromPath(string fieldPath) =>
        "#/properties/" + string.Join("/properties/", fieldPath.Split('/'));

    private static void ApplyStringHint(JsonObject schema, string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            schema[key] = value;
        }
    }

    private static string? ResolveControlOverride(
        PropertyInfo property,
        SchemaGenerationOptions options
    )
    {
        if (IsSecret(property, options))
        {
            return "secret";
        }

        return property.GetCustomAttribute<CfControlAttribute>()?.ControlType
            ?? Options(property)?.Control;
    }

    private static bool IsSecret(PropertyInfo property, SchemaGenerationOptions options)
    {
        if (
            property.GetCustomAttribute<CfSecretAttribute>() is not null
            || Options(property)?.Secret == true
        )
        {
            return true;
        }

        Type type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        return options.SecretTypeNames.Contains(type.Name);
    }

    private static void AddValidationConstraints(JsonObject schema, PropertyInfo property)
    {
        if (property.GetCustomAttribute<RangeAttribute>() is { } range)
        {
            if (TryToDouble(range.Minimum, out double min))
            {
                schema["minimum"] = min;
            }

            if (TryToDouble(range.Maximum, out double max))
            {
                schema["maximum"] = max;
            }
        }

        if (property.GetCustomAttribute<StringLengthAttribute>() is { } length)
        {
            if (length.MinimumLength > 0)
            {
                schema["minLength"] = length.MinimumLength;
            }

            schema["maxLength"] = length.MaximumLength;
        }

        if (property.GetCustomAttribute<RegularExpressionAttribute>() is { } pattern)
        {
            schema["pattern"] = pattern.Pattern;
        }
    }

    private static IEnumerable<PropertyInfo> GetSerializableProperties(
        Type type,
        SchemaGenerationOptions options
    )
    {
        List<(PropertyInfo Property, int Order, int Token)> selected = [];
        foreach (
            PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
        )
        {
            if (!IsSerializable(property, options))
            {
                continue;
            }

            int order =
                property.GetCustomAttribute<CfOrderAttribute>()?.Order
                ?? Options(property)?.Order
                ?? int.MaxValue;
            selected.Add((property, order, property.MetadataToken));
        }

        return selected.OrderBy(e => e.Order).ThenBy(e => e.Token).Select(e => e.Property);
    }

    private static bool IsSerializable(PropertyInfo property, SchemaGenerationOptions options)
    {
        if (property.GetIndexParameters().Length > 0 || !property.CanRead || !property.CanWrite)
        {
            return false;
        }

        if (
            property.GetCustomAttribute<CfIgnoreAttribute>() is not null
            || IsJsonIgnored(property)
            || Options(property)?.Ignore == true
        )
        {
            return false;
        }

        return options.IncludeObsolete || property.GetCustomAttribute<ObsoleteAttribute>() is null;
    }

    private static bool IsJsonIgnored(PropertyInfo property)
    {
        JsonIgnoreAttribute? ignore = property.GetCustomAttribute<JsonIgnoreAttribute>();
        return ignore is not null
            && ignore.Condition is JsonIgnoreCondition.Always or JsonIgnoreCondition.Never;
    }

    private static bool IsRequired(PropertyInfo property)
    {
        if (property.GetCustomAttribute<JsonRequiredAttribute>() is not null)
        {
            return true;
        }

        if (property.GetCustomAttribute<RequiredAttribute>() is not null)
        {
            return true;
        }

        return property
            .GetCustomAttributes()
            .Any(a =>
                string.Equals(a.GetType().Name, "RequiredMemberAttribute", StringComparison.Ordinal)
            );
    }

    private static string ResolvePropertyName(
        PropertyInfo property,
        SchemaGenerationOptions options
    )
    {
        string? explicitName = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name;
        return explicitName ?? options.PropertyNamingPolicy.ConvertName(property.Name);
    }

    private static string ResolveTitle(PropertyInfo property)
    {
        string? label =
            property.GetCustomAttribute<CfLabelAttribute>()?.Label
            ?? Options(property)?.Label
            ?? property.GetCustomAttribute<DisplayAttribute>()?.GetName();
        return label ?? Humanize(property.Name);
    }

    private static string? ResolveDescription(PropertyInfo property)
    {
        return property.GetCustomAttribute<CfDescriptionAttribute>()?.Description
            ?? Options(property)?.Description
            ?? property.GetCustomAttribute<DescriptionAttribute>()?.Description
            ?? property.GetCustomAttribute<DisplayAttribute>()?.GetDescription();
    }

    private static string DiscriminatorValue(JsonDerivedTypeAttribute derived)
    {
        object? value = derived.TypeDiscriminator;
        return value switch
        {
            string s => s,
            null => derived.DerivedType.Name,
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? derived.DerivedType.Name,
        };
    }

    private static IEnumerable<JsonDerivedTypeAttribute> GetDerivedTypes(Type baseType) =>
        baseType.GetCustomAttributes<JsonDerivedTypeAttribute>();

    private static bool HasPolymorphism(Type type) =>
        type.GetCustomAttributes<JsonDerivedTypeAttribute>().Any();

    private static bool IsFreeformJson(Type type) =>
        typeof(JsonNode).IsAssignableFrom(type) || type == typeof(JsonElement);

    private static bool IsIntegerType(Type type) =>
        type == typeof(int)
        || type == typeof(long)
        || type == typeof(short)
        || type == typeof(byte)
        || type == typeof(sbyte)
        || type == typeof(uint)
        || type == typeof(ulong)
        || type == typeof(ushort);

    private static bool TryGetDictionaryTypes(Type type, out Type keyType, out Type valueType)
    {
        foreach (Type candidate in Interfaces(type))
        {
            if (!candidate.IsGenericType)
            {
                continue;
            }

            Type definition = candidate.GetGenericTypeDefinition();
            if (
                definition == typeof(IDictionary<,>)
                || definition == typeof(IReadOnlyDictionary<,>)
            )
            {
                Type[] args = candidate.GetGenericArguments();
                keyType = args[0];
                valueType = args[1];
                return true;
            }
        }

        keyType = typeof(object);
        valueType = typeof(object);
        return false;
    }

    private static bool TryGetEnumerableElementType(Type type, out Type elementType)
    {
        if (type == typeof(string))
        {
            elementType = typeof(object);
            return false;
        }

        if (type.IsArray)
        {
            elementType = type.GetElementType() ?? typeof(object);
            return true;
        }

        foreach (Type candidate in Interfaces(type))
        {
            if (
                candidate.IsGenericType
                && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            )
            {
                elementType = candidate.GetGenericArguments()[0];
                return true;
            }
        }

        elementType = typeof(object);
        return false;
    }

    private static IEnumerable<Type> Interfaces(Type type)
    {
        if (type.IsInterface)
        {
            yield return type;
        }

        foreach (Type i in type.GetInterfaces())
        {
            yield return i;
        }
    }

    private static bool TryToDouble(object? value, out double result)
    {
        if (value is null)
        {
            result = 0;
            return false;
        }

        try
        {
            result = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            return true;
        }
        catch (FormatException)
        {
            result = 0;
            return false;
        }
        catch (InvalidCastException)
        {
            result = 0;
            return false;
        }
    }

    private static string Humanize(string name)
    {
        StringBuilder builder = new(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]))
            {
                builder.Append(' ');
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Builds the JsonForms navigation from <c>[CfGroup]</c> and <c>[CfCategory]</c> on
    /// the root type's properties. With only categories the result is a single-level
    /// <c>Categorization</c> (sidebar tabs). With groups it is two-level: groups form the
    /// sidebar and each group's categories become a nested <c>Categorization</c> of
    /// sub-tabs. Returns null when neither is declared, leaving a flat page.
    /// </summary>
    private static JsonObject? BuildCategorization(
        Type rootType,
        JsonObject schema,
        SchemaGenerationOptions options
    )
    {
        List<CategoryMember> roots =
        [
            .. GetSerializableProperties(rootType, options)
                .Select(p => new CategoryMember(
                    ResolvePropertyName(p, options),
                    p.GetCustomAttribute<CfGroupAttribute>()?.Group ?? Options(p)?.Group,
                    p.GetCustomAttribute<CfCategoryAttribute>()?.Category ?? Options(p)?.Category,
                    p.GetCustomAttribute<CfSectionAttribute>()?.Section ?? Options(p)?.Section,
                    p.GetCustomAttribute<CfRowAttribute>()?.Row
                )),
        ];

        bool anyGroup = roots.Exists(r => r.GroupName is not null);
        bool anyCategory = roots.Exists(r => r.CategoryName is not null);
        bool anySection = roots.Exists(r => r.Section is not null);
        bool anyRow = roots.Exists(r => r.Row is not null);
        if (!anyGroup && !anyCategory && !anySection && !anyRow)
        {
            return null;
        }

        JsonObject properties = schema["properties"] as JsonObject ?? [];
        return anyGroup
            ? BuildGroupedCategorization(roots, properties)
            : BuildFlatCategorization(roots, properties);
    }

    private static JsonObject BuildFlatCategorization(
        List<CategoryMember> roots,
        JsonObject properties
    )
    {
        List<string> order = [];
        Dictionary<string, List<CategoryMember>> byCategory = new(StringComparer.Ordinal);

        foreach (CategoryMember member in roots)
        {
            GetOrAdd(byCategory, order, member.CategoryName ?? "General").Add(member);
        }

        return Categorization([
            .. order.Select(label =>
                Category(label, BuildCategoryElements(byCategory[label], properties))
            ),
        ]);
    }

    private static JsonObject BuildGroupedCategorization(
        List<CategoryMember> roots,
        JsonObject properties
    )
    {
        List<string> groupOrder = [];
        Dictionary<
            string,
            (List<string> TabOrder, Dictionary<string, List<CategoryMember>> Tabs)
        > groups = new(StringComparer.Ordinal);

        foreach (CategoryMember member in roots)
        {
            string groupLabel = member.GroupName ?? "General";
            if (!groups.TryGetValue(groupLabel, out var groupEntry))
            {
                groupEntry = (
                    [],
                    new Dictionary<string, List<CategoryMember>>(StringComparer.Ordinal)
                );
                groups[groupLabel] = groupEntry;
                groupOrder.Add(groupLabel);
            }

            GetOrAdd(groupEntry.Tabs, groupEntry.TabOrder, member.CategoryName ?? "General")
                .Add(member);
        }

        JsonArray groupCategories = [];
        foreach (string groupLabel in groupOrder)
        {
            (List<string> tabOrder, Dictionary<string, List<CategoryMember>> tabs) = groups[
                groupLabel
            ];

            // A single sub-tab needs no tab strip: render its content directly.
            if (tabOrder.Count == 1)
            {
                groupCategories.Add(
                    Category(groupLabel, BuildCategoryElements(tabs[tabOrder[0]], properties))
                );
                continue;
            }

            JsonArray subTabs =
            [
                .. tabOrder.Select(tab =>
                    Category(tab, BuildCategoryElements(tabs[tab], properties))
                ),
            ];
            groupCategories.Add(Category(groupLabel, [Categorization(subTabs)]));
        }

        return Categorization(groupCategories);
    }

    /// <summary>
    /// Builds a category's UI elements, grouping members that share a <c>[CfSection]</c>
    /// into a titled <c>Group</c> box (in first-appearance order); sectionless members
    /// render as bare controls. Consecutive members that share a <c>[CfRow]</c> id within the
    /// same target are wrapped in a <c>HorizontalLayout</c> so they render side by side.
    /// </summary>
    private static JsonArray BuildCategoryElements(
        List<CategoryMember> members,
        JsonObject properties
    )
    {
        JsonArray elements = [];
        Dictionary<string, JsonArray> sections = new(StringComparer.Ordinal);

        // An open horizontal run: the target list it belongs to, its row id, and its controls.
        JsonArray? rowTarget = null;
        string? rowId = null;
        JsonArray? rowElements = null;

        void FlushRow()
        {
            if (rowElements is not null && rowTarget is not null)
            {
                rowTarget.Add(HorizontalLayout(rowElements));
            }

            rowTarget = null;
            rowId = null;
            rowElements = null;
        }

        foreach (CategoryMember member in members)
        {
            JsonArray target;
            if (member.Section is null)
            {
                target = elements;
            }
            else if (sections.TryGetValue(member.Section, out JsonArray? groupElements))
            {
                target = groupElements;
            }
            else
            {
                groupElements = [];
                sections[member.Section] = groupElements;
                elements.Add(Group(member.Section, groupElements));
                target = groupElements;
            }

            if (member.Row is { Length: > 0 } row)
            {
                if (
                    !(
                        rowElements is not null
                        && rowTarget == target
                        && string.Equals(rowId, row, StringComparison.Ordinal)
                    )
                )
                {
                    FlushRow();
                    rowTarget = target;
                    rowId = row;
                    rowElements = [];
                }

                AppendLeafControls(rowElements!, properties, member.Name);
            }
            else
            {
                FlushRow();
                AppendLeafControls(target, properties, member.Name);
            }
        }

        FlushRow();
        return elements;
    }

    private static List<CategoryMember> GetOrAdd(
        Dictionary<string, List<CategoryMember>> map,
        List<string> order,
        string label
    )
    {
        if (!map.TryGetValue(label, out List<CategoryMember>? members))
        {
            members = [];
            map[label] = members;
            order.Add(label);
        }

        return members;
    }

    private static void AppendLeafControls(JsonArray controls, JsonObject properties, string name)
    {
        if (properties[name] is not JsonObject propNode)
        {
            return;
        }

        foreach (string leaf in LeafKeys(propNode, name))
        {
            controls.Add(
                new JsonObject { ["type"] = "Control", ["scope"] = JsonFormsScope.ToScope(leaf) }
            );
        }
    }

    private static JsonObject Category(string label, JsonArray elements) =>
        new()
        {
            ["type"] = "Category",
            ["label"] = label,
            ["elements"] = elements,
        };

    private static JsonObject Group(string label, JsonArray elements) =>
        new()
        {
            ["type"] = "Group",
            ["label"] = label,
            ["elements"] = elements,
        };

    private static JsonObject Categorization(JsonArray elements) =>
        new() { ["type"] = "Categorization", ["elements"] = elements };

    private static JsonObject HorizontalLayout(JsonArray elements) =>
        new() { ["type"] = "HorizontalLayout", ["elements"] = elements };

    private readonly record struct CategoryMember(
        string Name,
        string? GroupName,
        string? CategoryName,
        string? Section,
        string? Row
    );

    /// <summary>
    /// Enumerates the leaf field keys a schema node contributes, mirroring the parser's
    /// flattening: a plain object with no explicit control recurses into its properties,
    /// everything else (scalars, maps, arrays, oneof, coded objects) is a single leaf.
    /// </summary>
    private static IEnumerable<string> LeafKeys(JsonObject node, string prefix)
    {
        bool hasControl = !string.IsNullOrEmpty(
            (node["x-control"] as JsonValue)?.GetValue<string>()
        );
        bool isPlainObject =
            (
                node["type"] is null
                || string.Equals(
                    (node["type"] as JsonValue)?.GetValue<string>(),
                    "object",
                    StringComparison.Ordinal
                )
            )
            && node["properties"] is JsonObject;

        if (hasControl || !isPlainObject)
        {
            yield return prefix;
            yield break;
        }

        JsonObject props = (JsonObject)node["properties"]!;
        foreach (KeyValuePair<string, JsonNode?> entry in props)
        {
            if (entry.Value is JsonObject child)
            {
                foreach (string key in LeafKeys(child, prefix + "/" + entry.Key))
                {
                    yield return key;
                }
            }
        }
    }

    private static void DeepMerge(JsonObject target, JsonObject overlay)
    {
        foreach (KeyValuePair<string, JsonNode?> entry in overlay)
        {
            if (
                entry.Value is JsonObject overlayChild
                && target[entry.Key] is JsonObject targetChild
            )
            {
                DeepMerge(targetChild, overlayChild);
            }
            else
            {
                target[entry.Key] = entry.Value?.DeepClone();
            }
        }
    }
}
