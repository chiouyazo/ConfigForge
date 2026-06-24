using ConfigForge.Abstractions;
using ConfigForge.Core.Documents;
using ConfigForge.Core.Schema;
using Xunit;

namespace ConfigForge.Core.Tests;

public sealed class GeneratorTests
{
    private static ConfigSchema Schema() => new JsonFormsSchemaParser().Parse(CanonicalSchema.Json);

    [Fact]
    public void GenerateExample_PopulatesEveryField()
    {
        var generator = new ConfigDocumentGenerator();
        ConfigSchema schema = Schema();

        ConfigDocument example = generator.GenerateExample(schema);

        foreach (string key in schema.Fields.Keys)
        {
            Assert.True(example.ContainsKey(key), $"Expected example to contain '{key}'.");
        }
    }

    [Fact]
    public void GenerateExample_RequiredFieldsHaveValues()
    {
        var generator = new ConfigDocumentGenerator();

        ConfigDocument example = generator.GenerateExample(Schema());

        Assert.NotNull(example["endpoint_url"]);
        Assert.NotNull(example["api_secret"]);
    }

    [Fact]
    public void GenerateExample_UsesDeclaredDefaults()
    {
        var generator = new ConfigDocumentGenerator();

        ConfigDocument example = generator.GenerateExample(Schema());

        Assert.Equal(60L, example["interval_minutes"]);
        Assert.Equal(true, example["enabled"]);
    }

    [Fact]
    public void GenerateEmpty_OnlyContainsFieldsWithDeclaredDefaults()
    {
        var generator = new ConfigDocumentGenerator();

        ConfigDocument empty = generator.GenerateEmpty(Schema());

        Assert.True(empty.ContainsKey("interval_minutes"));
        Assert.True(empty.ContainsKey("enabled"));
        Assert.False(empty.ContainsKey("endpoint_url"));
        Assert.False(empty.ContainsKey("api_secret"));
        Assert.False(empty.ContainsKey("target_channel"));
    }

    [Fact]
    public void GenerateEmpty_ParsesAsInvalidWithMissingRequiredKeys()
    {
        var generator = new ConfigDocumentGenerator();
        var engine = new ConfigDocumentEngine();
        ConfigSchema schema = Schema();

        ConfigDocument empty = generator.GenerateEmpty(schema);
        ConfigDocumentParseResult result = engine.Parse(engine.Serialize(empty), schema);

        Assert.False(result.IsValid);
        Assert.Contains("endpoint_url", result.MissingRequiredKeys);
        Assert.Contains("api_secret", result.MissingRequiredKeys);
    }

    /// <summary>
    /// The spec guarantees the <c>GenerateExample</c> result parses as valid. For
    /// <c>endpoint_url</c> (pattern <c>^https?://.+</c>) the generator must pick a
    /// pattern-satisfying value rather than the literal "example", so the example
    /// document validates.
    /// </summary>
    [Fact]
    public void GenerateExample_HonoursPattern_SoExampleIsValid()
    {
        var generator = new ConfigDocumentGenerator();
        var engine = new ConfigDocumentEngine();
        ConfigSchema schema = Schema();

        ConfigDocument example = generator.GenerateExample(schema);
        ConfigDocumentParseResult result = engine.Parse(engine.Serialize(example), schema);

        Assert.Matches("^https?://.+", (string)example["endpoint_url"]!);
        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.InvalidValues, v => v.Key == "endpoint_url");
    }
}
