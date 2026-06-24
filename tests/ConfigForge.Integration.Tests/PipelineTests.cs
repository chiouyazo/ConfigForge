using ConfigForge.Abstractions;
using ConfigForge.Core.Documents;
using ConfigForge.Core.Schema;
using Xunit;

namespace ConfigForge.Integration.Tests;

/// <summary>
/// End-to-end document pipeline: parse schema, generate documents, round-trip
/// through serialize/parse.
/// </summary>
public sealed class PipelineTests
{
    private static ConfigSchema Schema() => new JsonFormsSchemaParser().Parse(CanonicalSchema.Json);

    [Fact]
    public void GenerateExample_SerializeReparse_RoundTripsValues()
    {
        ConfigSchema schema = Schema();
        var generator = new ConfigDocumentGenerator();
        var engine = new ConfigDocumentEngine();

        ConfigDocument example = generator.GenerateExample(schema);
        string serialized = engine.Serialize(example);
        ConfigDocumentParseResult reparsed = engine.Parse(serialized, schema);
        ConfigDocument roundTripped = reparsed.Document;

        foreach (string key in example.Keys)
        {
            Assert.True(
                roundTripped.ContainsKey(key),
                $"Round-tripped document is missing '{key}'."
            );
            Assert.Equal(example[key], roundTripped[key]);
        }

        Assert.Equal(60L, roundTripped["interval_minutes"]);
        Assert.Equal(true, roundTripped["enabled"]);
    }

    [Fact]
    public void GenerateEmpty_SerializeParse_MissesRequiredKeys()
    {
        ConfigSchema schema = Schema();
        var generator = new ConfigDocumentGenerator();
        var engine = new ConfigDocumentEngine();

        ConfigDocument empty = generator.GenerateEmpty(schema);
        ConfigDocumentParseResult result = engine.Parse(engine.Serialize(empty), schema);

        Assert.False(result.IsValid);
        Assert.Contains("endpoint_url", result.MissingRequiredKeys);
        Assert.Contains("api_secret", result.MissingRequiredKeys);
    }

    [Fact]
    public void Parse_RecognisesSchemaIdentityAndFields()
    {
        ConfigSchema schema = Schema();

        Assert.Equal("example-product", schema.Id);
        Assert.Equal("Example Product", schema.Name);
        Assert.Contains("endpoint_url", schema.Fields.Keys);
        Assert.Contains("enabled", schema.Fields.Keys);
        Assert.Equal(2, schema.Categories.Count);
    }
}
