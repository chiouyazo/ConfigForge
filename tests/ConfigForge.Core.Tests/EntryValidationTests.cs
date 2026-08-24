using ConfigForge.Abstractions;
using ConfigForge.Core.Documents;
using ConfigForge.Core.Schema;
using Xunit;

namespace ConfigForge.Core.Tests;

/// <summary>
/// Required-validation and untracked-stripping must reach inside map entries and oneof variants,
/// not just the flat top-level key space.
/// </summary>
public sealed class EntryValidationTests
{
    // connectors: map(uuid) of a oneof variant that requires "endpoints" and has an untracked
    // "connectionValid" status field.
    private const string SchemaJson = """
        {
          "schema": {
            "type": "object",
            "properties": {
              "connectors": {
                "type": "object",
                "x-key-format": "uuid",
                "additionalProperties": {
                  "oneOf": [
                    {
                      "type": "object",
                      "required": ["endpoints"],
                      "properties": {
                        "type": { "type": "string", "const": "http" },
                        "endpoints": { "type": "array", "items": { "type": "string" }, "title": "Sync channels" },
                        "connectionValid": { "type": "string", "title": "Connection valid", "x-tracked": false }
                      }
                    }
                  ]
                }
              }
            }
          },
          "x-cf": { "id": "ev", "name": "EntryValidation" }
        }
        """;

    private static ConfigSchema Schema() => new JsonFormsSchemaParser().Parse(SchemaJson);

    private const string Guid = "55555555-5555-5555-5555-555555555555";

    [Fact]
    public void Parse_RequiredInsideOneOfEntry_IsReportedMissing()
    {
        // A connector entry that omits the required endpoints.
        string doc = $$"""
            { "connectors": { "{{Guid}}": { "type": "http", "connectionValid": "ok" } } }
            """;

        ConfigDocumentParseResult result = new ConfigDocumentEngine().Parse(doc, Schema());

        Assert.False(result.IsValid);
        Assert.Contains(
            $"connectors/{Guid}/endpoints",
            result.MissingRequiredKeys,
            StringComparer.Ordinal
        );
    }

    [Fact]
    public void Serialize_StripsUntrackedFieldInsideEntry()
    {
        var document = new ConfigDocument();
        document["connectors"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [Guid] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["type"] = "http",
                ["endpoints"] = new List<object?> { "a" },
                ["connectionValid"] = "ok",
            },
        };

        string json = new ConfigDocumentEngine().Serialize(document, Schema());

        Assert.DoesNotContain("connectionValid", json, StringComparison.Ordinal);
        // The tracked siblings survive.
        Assert.Contains("endpoints", json, StringComparison.Ordinal);
    }
}
