using ConfigForge.Abstractions;
using ConfigForge.Core.Documents;
using ConfigForge.Core.Schema;
using Xunit;

namespace ConfigForge.Core.Tests;

public sealed class NestedSchemaTests
{
    private const string NestedJson = """
        {
          "schema": {
            "type": "object",
            "definitions": {
              "jobSchedule": {
                "type": "object",
                "properties": {
                  "startTime": { "type": "string" },
                  "enabled": { "type": "boolean" }
                }
              }
            },
            "properties": {
              "ExchangeLock": {
                "type": "object",
                "properties": {
                  "ConnectionString": { "type": "string" },
                  "Timeout": { "type": "integer" }
                },
                "required": ["ConnectionString"]
              },
              "Sync": {
                "type": "object",
                "properties": {
                  "ExportMasterData": { "$ref": "#/definitions/jobSchedule" }
                }
              },
              "Targets": {
                "type": "array",
                "items": {
                  "type": "object",
                  "properties": {
                    "Name": { "type": "string" },
                    "Endpoint": { "type": "string" },
                    "Headers": {
                      "type": "object",
                      "additionalProperties": { "type": "string" }
                    }
                  }
                }
              }
            }
          },
          "x-cf": { "id": "nested-test", "name": "Nested", "version": "1.0.0" }
        }
        """;

    private static ConfigSchema Parse() => new JsonFormsSchemaParser().Parse(NestedJson);

    [Fact]
    public void Parse_FlattensNestedObjectIntoPathKeys()
    {
        ConfigSchema schema = Parse();

        Assert.Contains("ExchangeLock/ConnectionString", schema.Fields.Keys);
        Assert.Contains("ExchangeLock/Timeout", schema.Fields.Keys);
        Assert.DoesNotContain("ExchangeLock", schema.Fields.Keys);
    }

    [Fact]
    public void Parse_CarriesRequiredFromNestedObject()
    {
        ConfigSchema schema = Parse();
        Assert.True(schema.Fields["ExchangeLock/ConnectionString"].Required);
        Assert.False(schema.Fields["ExchangeLock/Timeout"].Required);
    }

    [Fact]
    public void Parse_ResolvesLocalRef()
    {
        ConfigSchema schema = Parse();

        Assert.Contains("Sync/ExportMasterData/startTime", schema.Fields.Keys);
        Assert.Equal("checkbox", schema.Fields["Sync/ExportMasterData/enabled"].ControlType);
    }

    [Fact]
    public void Parse_ArrayOfObjectsBecomesArrayObjectWithChildren()
    {
        ConfigSchema schema = Parse();
        FieldDefinition targets = schema.Fields["Targets"];

        Assert.Equal("arrayobject", targets.ControlType);
        Assert.Contains(targets.Children, c => c.Key == "Name");
        Assert.Contains(targets.Children, c => c.Key == "Endpoint");
    }

    [Fact]
    public void Parse_AdditionalPropertiesBecomesMap()
    {
        ConfigSchema schema = Parse();
        FieldDefinition headers = schema.Fields["Targets"].Children.Single(c => c.Key == "Headers");

        Assert.Equal("map", headers.ControlType);
        Assert.NotNull(headers.ValueField);
        Assert.Equal("text", headers.ValueField!.ControlType);
    }

    [Fact]
    public void Document_PathAccessReadsAndWritesNestedValues()
    {
        ConfigDocument document = new();
        document["ExchangeLock/ConnectionString"] = "Server=db;";

        Assert.Equal("Server=db;", document["ExchangeLock/ConnectionString"]);
        Assert.True(document["ExchangeLock"] is IDictionary<string, object?>);
        Assert.True(document.ContainsKey("ExchangeLock/ConnectionString"));
    }

    [Fact]
    public void Document_PathAccessIndexesIntoArrays()
    {
        ConfigDocument document = new();
        document["Targets"] = new List<object?>
        {
            new Dictionary<string, object?>(StringComparer.Ordinal),
        };
        document["Targets/0/Name"] = "otlp";

        Assert.Equal("otlp", document["Targets/0/Name"]);
    }

    [Fact]
    public void Engine_RoundTripsNestedDocument()
    {
        ConfigSchema schema = Parse();
        const string json = """
            {
              "ExchangeLock": { "ConnectionString": "Server=db;", "Timeout": 30 },
              "Targets": [ { "Name": "otlp", "Headers": { "Authorization": "Bearer x" } } ]
            }
            """;

        ConfigDocumentEngine engine = new();
        ConfigDocumentParseResult result = engine.Parse(json, schema);

        Assert.Empty(result.UnknownKeys);
        Assert.Equal("Server=db;", result.Document["ExchangeLock/ConnectionString"]);
        Assert.Equal("Bearer x", result.Document["Targets/0/Headers/Authorization"]);

        string serialized = engine.Serialize(result.Document);
        ConfigDocumentParseResult reparsed = engine.Parse(serialized, schema);
        Assert.Equal("Server=db;", reparsed.Document["ExchangeLock/ConnectionString"]);
        Assert.Equal("Bearer x", reparsed.Document["Targets/0/Headers/Authorization"]);
    }

    [Fact]
    public void Generator_BuildsNestedExample()
    {
        ConfigSchema schema = Parse();
        ConfigDocument document = new ConfigDocumentGenerator().GenerateExample(schema);

        Assert.True(document["ExchangeLock"] is IDictionary<string, object?>);
        Assert.NotNull(document["ExchangeLock/ConnectionString"]);
        Assert.True(document["Targets"] is IList<object?> list && list.Count > 0);
        Assert.True(document["Targets/0/Headers"] is IDictionary<string, object?>);
    }
}
