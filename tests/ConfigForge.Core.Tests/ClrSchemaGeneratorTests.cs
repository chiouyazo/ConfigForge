using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ConfigForge.Abstractions.Annotations;
using ConfigForge.Core.Schema;
using ConfigForge.Core.Schema.Generation;
using Xunit;

namespace ConfigForge.Core.Tests;

/// <summary>
/// The reflection schema generator: it emits camelCase names, maps CLR shapes to
/// the right control types, and - crucially - carries inline control hints (like
/// <c>secret</c>) into oneof variants and map values, verified by round-tripping the
/// generated document through the real <see cref="JsonFormsSchemaParser"/>.
/// </summary>
public sealed class ClrSchemaGeneratorTests
{
    private sealed record ProtectedValue(string Value);

    private enum Speed
    {
        Fast,
        DarkSlow,
    }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(PrimaryVariant), "primary")]
    [JsonDerivedType(typeof(SecondaryVariant), "secondary")]
    private abstract record VariantBase
    {
        public string? Name { get; init; }
    }

    private sealed record PrimaryVariant : VariantBase
    {
        public required string Url { get; init; }
        public required ProtectedValue ClientSecret { get; init; }
    }

    private sealed record SecondaryVariant : VariantBase
    {
        public required string Url { get; init; }

        [CfSecret]
        public required string AccessToken { get; init; }
    }

    private sealed record Item
    {
        public string? Label { get; init; }
        public int Count { get; init; }
    }

    private sealed record SampleConfig
    {
        public string? InstanceName { get; init; }

        [CfSecret]
        public string? Password { get; init; }

        public Speed Speed { get; init; }

        [CfControl("textarea")]
        public string? Notes { get; init; }

        [CfUntracked]
        public string? TestEmail { get; init; }

        public IReadOnlyList<Item> Items { get; init; } = [];

        public IDictionary<Guid, VariantBase> Providers { get; init; } =
            new Dictionary<Guid, VariantBase>();

        public JsonNode? Args { get; init; }

        [Obsolete("gone")]
        public string? Legacy { get; init; }
    }

    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static ConfigSchema Generate()
    {
        SchemaGenerationOptions options = new()
        {
            Id = "sample",
            Name = "Sample",
            Version = "1",
        };
        // A library secret wrapper recognised by type name (no [CfSecret] needed).
        options.SecretTypeNames.Add(nameof(ProtectedValue));
        string json = new ClrSchemaGenerator().Generate<SampleConfig>(options);
        return new JsonFormsSchemaParser().Parse(json);
    }

    [Fact]
    public void MapsScalarControlTypes_WithCamelCaseKeys()
    {
        ConfigSchema schema = Generate();

        Assert.Equal("text", schema.Fields["instanceName"].ControlType);
        Assert.Equal("secret", schema.Fields["password"].ControlType);
        Assert.Equal("textarea", schema.Fields["notes"].ControlType);
        Assert.Equal("select", schema.Fields["speed"].ControlType);
        Assert.Equal("code", schema.Fields["args"].ControlType);
    }

    [Fact]
    public void EnumValues_UseCamelCaseNamingPolicy()
    {
        string json = new ClrSchemaGenerator().Generate<SampleConfig>(new() { Id = "sample" });
        JsonNode root = JsonNode.Parse(json)!;
        JsonArray values = (JsonArray)root["schema"]!["properties"]!["speed"]!["enum"]!;

        Assert.Equal(["fast", "darkSlow"], values.Select(v => v!.GetValue<string>()));
    }

    [Fact]
    public void UntrackedAttribute_MarksFieldUntracked()
    {
        ConfigSchema schema = Generate();
        Assert.False(schema.Fields["testEmail"].Tracked);
        Assert.True(schema.Fields["instanceName"].Tracked);
    }

    [Fact]
    public void ObsoleteProperty_IsSkipped()
    {
        ConfigSchema schema = Generate();
        Assert.False(schema.Fields.ContainsKey("legacy"));
    }

    [Fact]
    public void ObjectCollection_BecomesArrayObjectWithChildren()
    {
        ConfigSchema schema = Generate();
        FieldDefinition items = schema.Fields["items"];

        Assert.Equal("arrayobject", items.ControlType);
        Assert.Contains(items.Children, c => c.Key == "label");
        Assert.Contains(items.Children, c => c.Key == "count");
    }

    [Fact]
    public void PolymorphicDictionary_BecomesMapOfOneOf_WithDiscriminatorAndSecrets()
    {
        ConfigSchema schema = Generate();
        FieldDefinition providers = schema.Fields["providers"];

        Assert.Equal("map", providers.ControlType);

        FieldDefinition value = Assert.IsType<FieldDefinition>(providers.ValueField);
        Assert.Equal("oneof", value.ControlType);
        Assert.Equal("type", value.DiscriminatorKey);

        OneOfVariant primaryVariant = value.OneOfVariants.Single(v =>
            v.DiscriminatorValue == "primary"
        );
        FieldDefinition clientSecret = primaryVariant.Children.Single(c => c.Key == "clientSecret");
        // The secret hint (from a library secret type name) survived into the variant.
        Assert.Equal("secret", clientSecret.ControlType);

        OneOfVariant secondaryVariant = value.OneOfVariants.Single(v =>
            v.DiscriminatorValue == "secondary"
        );
        FieldDefinition accessToken = secondaryVariant.Children.Single(c => c.Key == "accessToken");
        Assert.Equal("secret", accessToken.ControlType);
    }

    [Fact]
    public void GeneratedKeys_MatchSystemTextJsonSerialization()
    {
        // Instantiating the sample graph also proves the generated property keys line
        // up with what System.Text.Json actually writes (camelCase + "type" discriminator).
        SampleConfig config = new()
        {
            InstanceName = "acme",
            Password = "secret",
            Speed = Speed.DarkSlow,
            Items = [new Item { Label = "a", Count = 1 }],
            Providers = new Dictionary<Guid, VariantBase>
            {
                [Guid.Empty] = new PrimaryVariant
                {
                    Name = "sw",
                    Url = "https://x",
                    ClientSecret = new ProtectedValue("cs"),
                },
                [Guid.Parse("00000000-0000-0000-0000-000000000001")] = new SecondaryVariant
                {
                    Name = "sf",
                    Url = "https://y",
                    AccessToken = "tok",
                },
            },
        };

        JsonObject serialized = (JsonObject)
            JsonSerializer.SerializeToNode(config, CamelCaseOptions)!;

        Assert.True(serialized.ContainsKey("instanceName"));
        Assert.True(serialized.ContainsKey("providers"));

        JsonObject firstShop = (JsonObject)((JsonObject)serialized["providers"]!).First().Value!;
        Assert.Equal("primary", firstShop["type"]!.GetValue<string>());
        Assert.True(firstShop.ContainsKey("url"));
        Assert.True(firstShop.ContainsKey("clientSecret"));
    }

    private sealed record CategorizedConfig
    {
        [CfCategory("General")]
        public string? InstanceName { get; init; }

        [CfCategory("Connection")]
        public Endpoint Endpoint { get; init; } = new();

        [CfCategory("General")]
        public bool Enabled { get; init; }
    }

    private sealed record Endpoint
    {
        public string? Host { get; init; }
        public int Port { get; init; }
    }

    [Fact]
    public void Categories_ProduceTabsGroupingFlattenedLeafControls()
    {
        Assert.NotNull(new CategorizedConfig());
        string json = new ClrSchemaGenerator().Generate<CategorizedConfig>(new() { Id = "cat" });
        JsonObject root = (JsonObject)JsonNode.Parse(json)!;

        JsonObject ui = (JsonObject)root["uiSchema"]!;
        Assert.Equal("Categorization", ui["type"]!.GetValue<string>());

        JsonArray cats = (JsonArray)ui["elements"]!;
        Assert.Equal(["General", "Connection"], cats.Select(c => c!["label"]!.GetValue<string>()));

        // The nested Endpoint object flattened into two leaf controls under "Connection".
        JsonArray connection = (JsonArray)cats[1]!["elements"]!;
        List<string> scopes = [.. connection.Select(c => c!["scope"]!.GetValue<string>())];
        Assert.Contains("#/properties/endpoint/properties/host", scopes);
        Assert.Contains("#/properties/endpoint/properties/port", scopes);

        // Parsing the whole document yields the two tabs.
        ConfigSchema schema = new JsonFormsSchemaParser().Parse(json);
        Assert.Equal(["General", "Connection"], schema.Categories.Select(c => c.Label));
    }

    private sealed record GroupedConfig
    {
        [CfGroup("Connection")]
        [CfCategory("Endpoint")]
        public string? Url { get; init; }

        [CfGroup("Connection")]
        [CfCategory("Auth")]
        public string? Token { get; init; }

        [CfGroup("Advanced")]
        public bool Verbose { get; init; }
    }

    [Fact]
    public void Groups_ProduceTwoLevelNavigation()
    {
        Assert.NotNull(new GroupedConfig());
        string json = new ClrSchemaGenerator().Generate<GroupedConfig>(new() { Id = "g" });
        JsonObject root = (JsonObject)JsonNode.Parse(json)!;

        JsonArray groups = (JsonArray)root["uiSchema"]!["elements"]!;
        Assert.Equal(
            ["Connection", "Advanced"],
            groups.Select(g => g!["label"]!.GetValue<string>())
        );

        // The "Connection" group holds a nested Categorization with the two sub-tabs.
        JsonObject nested = (JsonObject)((JsonArray)groups[0]!["elements"]!)[0]!;
        Assert.Equal("Categorization", nested["type"]!.GetValue<string>());
        Assert.Equal(
            ["Endpoint", "Auth"],
            ((JsonArray)nested["elements"]!).Select(t => t!["label"]!.GetValue<string>())
        );

        // The "Advanced" group has a single sub-tab, so its controls render directly.
        JsonObject advancedFirst = (JsonObject)((JsonArray)groups[1]!["elements"]!)[0]!;
        Assert.Equal("Control", advancedFirst["type"]!.GetValue<string>());

        // The sidebar (top-level categories) lists the groups.
        ConfigSchema schema = new JsonFormsSchemaParser().Parse(json);
        Assert.Equal(["Connection", "Advanced"], schema.Categories.Select(c => c.Label));
    }

    private sealed record SectionedConfig
    {
        [CfCategory("Main")]
        [CfSection("Auth")]
        public string? User { get; init; }

        [CfCategory("Main")]
        [CfSection("Auth")]
        public string? Pass { get; init; }

        [CfCategory("Main")]
        [CfSection("Limits")]
        public int Max { get; init; }
    }

    [Fact]
    public void Sections_GroupControlsIntoGroupBoxes()
    {
        Assert.NotNull(new SectionedConfig());
        string json = new ClrSchemaGenerator().Generate<SectionedConfig>(new() { Id = "s" });
        JsonObject root = (JsonObject)JsonNode.Parse(json)!;

        JsonArray cats = (JsonArray)root["uiSchema"]!["elements"]!;
        JsonArray mainElements = (JsonArray)
            cats.Single(c => c!["label"]!.GetValue<string>() == "Main")!["elements"]!;

        List<JsonNode?> groups =
        [
            .. mainElements.Where(e => e!["type"]!.GetValue<string>() == "Group"),
        ];
        Assert.Equal(["Auth", "Limits"], groups.Select(g => g!["label"]!.GetValue<string>()));
        // The "Auth" section box holds both its controls.
        Assert.Equal(2, ((JsonArray)groups[0]!["elements"]!).Count);
    }

    [Fact]
    public void Overlay_DeepMergesOverGeneratedDocument()
    {
        SchemaGenerationOptions options = new()
        {
            Id = "sample",
            Overlay = new JsonObject
            {
                ["x-cf"] = new JsonObject
                {
                    ["controls"] = new JsonObject
                    {
                        ["instanceName"] = new JsonObject { ["type"] = "textarea" },
                    },
                },
            },
        };

        string json = new ClrSchemaGenerator().Generate<SampleConfig>(options);
        ConfigSchema schema = new JsonFormsSchemaParser().Parse(json);

        // Overlay control wins over the generated text control; other fields untouched.
        Assert.Equal("textarea", schema.Fields["instanceName"].ControlType);
        Assert.Equal("secret", schema.Fields["password"].ControlType);
    }

    private sealed record OptionsSample
    {
        [CfOptions(
            Group = "GroupA",
            Category = "CatA",
            Section = "SecA",
            Order = 5,
            Label = "Bulk Label",
            Tooltip = "a tip",
            ReadOnly = true
        )]
        public string? Full { get; init; }

        [CfOptions(Label = "From Bulk")]
        [CfLabel("From Individual")]
        public string? Overridden { get; init; }

        [CfOptions(Secret = true)]
        public string? Token { get; init; }
    }

    private static ConfigSchema GenerateOptionsSample()
    {
        Assert.NotNull(new OptionsSample());
        string json = new ClrSchemaGenerator().Generate<OptionsSample>(
            new SchemaGenerationOptions { Id = "opt", Name = "Opt" }
        );
        return new JsonFormsSchemaParser().Parse(json);
    }

    [Fact]
    public void CfOptions_AppliesEveryFacet()
    {
        ConfigSchema schema = GenerateOptionsSample();
        FieldDefinition full = schema.Fields["full"];

        Assert.Equal("Bulk Label", full.Title);
        Assert.Equal("a tip", full.Tooltip);
        Assert.Equal("SecA", full.Section);
        Assert.True(full.ReadOnly);
        // Group becomes a sidebar category.
        Assert.Contains(
            schema.Categories,
            c => string.Equals(c.Label, "GroupA", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void CfOptions_IndividualAttributeOverridesBulk()
    {
        ConfigSchema schema = GenerateOptionsSample();
        // A dedicated [CfLabel] wins over CfOptions.Label.
        Assert.Equal("From Individual", schema.Fields["overridden"].Title);
    }

    [Fact]
    public void CfOptions_SecretRendersSecretControl()
    {
        ConfigSchema schema = GenerateOptionsSample();
        Assert.Equal("secret", schema.Fields["token"].ControlType);
    }

    [CfCategoryMeta("General", Icon = "gear", Description = "The basics.")]
    [CfAction("ping", Label = "Ping", Category = "General", Icon = "signal")]
    private sealed record DecoratedConfig
    {
        [CfCategory("General")]
        public bool Enabled { get; init; }

        [CfCategory("General")]
        [CfEnableWhen("enabled")]
        public string? Endpoint { get; init; }

        [CfCategory("General")]
        public string? Mode { get; init; }

        [CfCategory("General")]
        [CfVisibleWhen("mode", "advanced")]
        public int? Threshold { get; init; }
    }

    private static ConfigSchema GenerateDecorated()
    {
        Assert.NotNull(new DecoratedConfig());
        string json = new ClrSchemaGenerator().Generate<DecoratedConfig>(
            new SchemaGenerationOptions { Id = "dec", Name = "Dec" }
        );
        return new JsonFormsSchemaParser().Parse(json);
    }

    [Fact]
    public void CfCategoryMeta_SetsIconAndDescription()
    {
        ConfigSchema schema = GenerateDecorated();
        CategoryElement general = schema.Categories.Single(c =>
            string.Equals(c.Label, "General", StringComparison.Ordinal)
        );

        Assert.Equal("gear", general.Icon);
        Assert.Equal("The basics.", general.Description);
    }

    [Fact]
    public void CfAction_BecomesSchemaAction()
    {
        ConfigSchema schema = GenerateDecorated();
        ActionDefinition ping = schema.Actions.Single(a =>
            string.Equals(a.ActionId, "ping", StringComparison.Ordinal)
        );

        Assert.Equal("Ping", ping.Label);
        Assert.Equal("General", ping.Category);
        Assert.Equal("signal", ping.Icon);
    }

    [Fact]
    public void CfEnableWhen_EmitsInlineEnableRule()
    {
        ConfigSchema schema = GenerateDecorated();
        JsonFormsRule rule = Assert.Single(schema.Fields["endpoint"].Rules);

        Assert.Equal(RuleEffect.Enable, rule.Effect);
        Assert.Equal("#/properties/enabled", rule.Condition.Scope);
    }

    [Fact]
    public void CfVisibleWhen_EmitsInlineShowRule()
    {
        ConfigSchema schema = GenerateDecorated();
        JsonFormsRule rule = Assert.Single(schema.Fields["threshold"].Rules);

        Assert.Equal(RuleEffect.Show, rule.Effect);
        Assert.Equal("#/properties/mode", rule.Condition.Scope);
    }

    private sealed record RowConfig
    {
        [CfRow("retry")]
        public int RetryCount { get; init; }

        [CfRow("retry")]
        public int RetryDelaySeconds { get; init; }

        [CfRow("retry")]
        public int TimeoutSeconds { get; init; }

        public string? Other { get; init; }
    }

    private sealed record ToggleSet
    {
        public bool Alpha { get; init; }
        public bool Beta { get; init; }
    }

    private sealed record FeatureBlock
    {
        [CfSection("Features")]
        public ToggleSet Features { get; init; } = new();

        public string? Note { get; init; }
    }

    private sealed record NestedBoxConfig
    {
        [CfCategory("Main")]
        public FeatureBlock ExchangeLock { get; init; } = new();
    }

    private sealed record RetryBlock
    {
        [CfRow("retry")]
        public int Count { get; init; }

        [CfRow("retry")]
        public int Delay { get; init; }
    }

    private sealed record NestedRowConfig
    {
        [CfCategory("Main")]
        public RetryBlock Retry { get; init; } = new();
    }

    [Fact]
    public void CfRow_WorksInsideNestedObject()
    {
        Assert.NotNull(new NestedRowConfig());
        Assert.NotNull(new RetryBlock());
        string json = new ClrSchemaGenerator().Generate<NestedRowConfig>(
            new SchemaGenerationOptions { Id = "nr" }
        );

        JsonArray mainElements = (JsonArray)
            JsonNode.Parse(json)!["uiSchema"]!["elements"]![0]!["elements"]!;

        // The [CfRow] on the nested Retry.Count/Delay produced a HorizontalLayout, even though
        // the row lives two levels deep — the same behaviour as a top-level row.
        JsonNode row = mainElements.First(e =>
            string.Equals(
                e!["type"]?.GetValue<string>(),
                "HorizontalLayout",
                StringComparison.Ordinal
            )
        )!;
        Assert.Equal(2, ((JsonArray)row["elements"]!).Count);
    }

    [Fact]
    public void CfSection_OnNestedObject_BecomesTitledBox()
    {
        Assert.NotNull(new NestedBoxConfig());
        Assert.NotNull(new FeatureBlock());
        Assert.NotNull(new ToggleSet());
        string json = new ClrSchemaGenerator().Generate<NestedBoxConfig>(
            new SchemaGenerationOptions { Id = "nb" }
        );

        JsonArray mainElements = (JsonArray)
            JsonNode.Parse(json)!["uiSchema"]!["elements"]![0]!["elements"]!;

        // The nested [CfSection] on ExchangeLock.Features produced a titled Group box...
        JsonNode box = mainElements.First(e =>
            string.Equals(e!["type"]?.GetValue<string>(), "Group", StringComparison.Ordinal)
        )!;
        Assert.Equal("Features", box["label"]!.GetValue<string>());

        JsonArray boxControls = (JsonArray)box["elements"]!;
        Assert.Equal(2, boxControls.Count);
        Assert.Contains(
            boxControls,
            c =>
                c!["scope"]!
                    .GetValue<string>()
                    .EndsWith("/properties/alpha", StringComparison.Ordinal)
        );

        // ...while the un-sectioned sibling stays a bare control outside the box.
        Assert.Contains(
            mainElements,
            e =>
                string.Equals(e!["type"]?.GetValue<string>(), "Control", StringComparison.Ordinal)
                && e["scope"]!
                    .GetValue<string>()
                    .EndsWith("/properties/note", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void CfRow_WrapsAdjacentFieldsInHorizontalLayout()
    {
        Assert.NotNull(new RowConfig());
        string json = new ClrSchemaGenerator().Generate<RowConfig>(
            new SchemaGenerationOptions { Id = "row" }
        );

        JsonNode ui = JsonNode.Parse(json)!["uiSchema"]!;
        JsonArray categoryElements = (JsonArray)ui["elements"]![0]!["elements"]!;

        JsonNode row = categoryElements.First(e =>
            string.Equals(
                e!["type"]?.GetValue<string>(),
                "HorizontalLayout",
                StringComparison.Ordinal
            )
        )!;
        JsonArray rowControls = (JsonArray)row["elements"]!;

        Assert.Equal(3, rowControls.Count);
        Assert.All(rowControls, c => Assert.Equal("Control", c!["type"]!.GetValue<string>()));
        // The non-row field is not swept into the horizontal layout.
        Assert.Contains(
            categoryElements,
            e => string.Equals(e!["type"]?.GetValue<string>(), "Control", StringComparison.Ordinal)
        );
    }
}
