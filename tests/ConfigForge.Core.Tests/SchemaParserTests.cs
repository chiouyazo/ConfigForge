using ConfigForge.Core.Schema;
using Xunit;

namespace ConfigForge.Core.Tests;

public sealed class SchemaParserTests
{
    private static ConfigSchema ParseCanonical() =>
        new JsonFormsSchemaParser().Parse(CanonicalSchema.Json);

    [Fact]
    public void Parse_SetsIdFromXcf()
    {
        ConfigSchema schema = ParseCanonical();
        Assert.Equal("example-product", schema.Id);
    }

    [Fact]
    public void Parse_ProducesTwoCategoriesWithExpectedLabels()
    {
        ConfigSchema schema = ParseCanonical();

        Assert.Equal(2, schema.Categories.Count);
        Assert.Equal("Connection", schema.Categories[0].Label);
        Assert.Equal("Synchronisation", schema.Categories[1].Label);
    }

    [Fact]
    public void Parse_FieldsContainAllFiveKeys()
    {
        ConfigSchema schema = ParseCanonical();

        Assert.Equal(5, schema.Fields.Count);
        Assert.Contains("endpoint_url", schema.Fields.Keys);
        Assert.Contains("api_secret", schema.Fields.Keys);
        Assert.Contains("target_channel", schema.Fields.Keys);
        Assert.Contains("interval_minutes", schema.Fields.Keys);
        Assert.Contains("enabled", schema.Fields.Keys);
    }

    [Fact]
    public void Parse_EndpointUrlIsRequired()
    {
        ConfigSchema schema = ParseCanonical();
        Assert.True(schema.Fields["endpoint_url"].Required);
    }

    [Fact]
    public void Parse_ApiSecretIsRequired()
    {
        ConfigSchema schema = ParseCanonical();
        Assert.True(schema.Fields["api_secret"].Required);
    }

    [Fact]
    public void Parse_IntervalMinutesNotRequiredWithDefault60()
    {
        ConfigSchema schema = ParseCanonical();
        FieldDefinition field = schema.Fields["interval_minutes"];

        Assert.False(field.Required);
        Assert.Equal(60L, field.DefaultValue);
    }

    [Theory]
    [InlineData("api_secret", "password")]
    [InlineData("interval_minutes", "slider")]
    [InlineData("enabled", "checkbox")]
    [InlineData("endpoint_url", "text")]
    [InlineData("target_channel", "select")]
    public void Parse_InfersControlType(string key, string expectedControlType)
    {
        ConfigSchema schema = ParseCanonical();
        Assert.Equal(expectedControlType, schema.Fields[key].ControlType);
    }

    [Fact]
    public void Parse_TargetChannelHasLoaderId()
    {
        ConfigSchema schema = ParseCanonical();
        Assert.Equal("example.loadChannels", schema.Fields["target_channel"].LoaderId);
    }

    [Fact]
    public void Parse_DisableRuleAttachedToTargetChannel()
    {
        ConfigSchema schema = ParseCanonical();
        FieldDefinition field = schema.Fields["target_channel"];

        JsonFormsRule rule = Assert.Single(field.Rules);
        Assert.Equal(RuleEffect.Disable, rule.Effect);
        Assert.Equal("#/properties/endpoint_url", rule.Condition.Scope);
    }

    [Fact]
    public void Parse_MalformedJsonThrowsSchemaParseException()
    {
        var parser = new JsonFormsSchemaParser();
        Assert.Throws<SchemaParseException>(() => parser.Parse("{not json"));
    }

    [Fact]
    public void Parse_MissingSchemaSectionThrowsSchemaParseException()
    {
        var parser = new JsonFormsSchemaParser();
        Assert.Throws<SchemaParseException>(() => parser.Parse("""{ "x-cf": { "id": "x" } }"""));
    }

    [Fact]
    public void Parse_ReadsXcfActions()
    {
        const string json = """
            {
              "schema": { "type": "object", "properties": { "a": { "type": "string" } } },
              "uiSchema": { "type": "VerticalLayout", "elements": [] },
              "x-cf": {
                "id": "p",
                "actions": [
                  { "actionId": "example.testConnection", "label": "Test Connection",
                    "icon": "plug", "variant": "primary",
                    "placement": { "category": "Connection", "position": "bottom" } }
                ]
              }
            }
            """;
        ConfigSchema schema = new JsonFormsSchemaParser().Parse(json);

        ActionDefinition action = Assert.Single(schema.Actions);
        Assert.Equal("example.testConnection", action.ActionId);
        Assert.Equal("Test Connection", action.Label);
        Assert.Equal("plug", action.Icon);
        Assert.Equal("primary", action.Variant);
        Assert.Equal("Connection", action.Category);
        Assert.Equal("bottom", action.Position);
    }

    [Theory]
    [InlineData("""{ "type": "integer", "minimum": 1, "maximum": 10 }""", "slider")]
    [InlineData("""{ "type": "integer", "minimum": 1 }""", "number")]
    [InlineData("""{ "type": "number" }""", "number")]
    public void Parse_InfersSliderOnlyWhenBoundedBothEnds(string propJson, string expected)
    {
        string json = $$"""
            {
              "schema": { "type": "object", "properties": { "n": {{propJson}} } },
              "uiSchema": { "type": "VerticalLayout", "elements": [] },
              "x-cf": { "id": "p" }
            }
            """;
        ConfigSchema schema = new JsonFormsSchemaParser().Parse(json);
        Assert.Equal(expected, schema.Fields["n"].ControlType);
    }
}
