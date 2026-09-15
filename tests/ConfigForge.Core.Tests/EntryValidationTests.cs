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

    // smtp: a nullable-object with a required child, same shape as an optional SMTP block.
    private const string NullableObjectSchemaJson = """
        {
          "schema": {
            "type": "object",
            "properties": {
              "smtp": {
                "type": ["object", "null"],
                "x-control": "nullable-object",
                "required": ["host"],
                "properties": {
                  "host": { "type": "string", "title": "Host" }
                }
              }
            }
          },
          "x-cf": { "id": "ev-null", "name": "EntryValidationNullableObject" }
        }
        """;

    private static ConfigSchema NullableObjectSchema() =>
        new JsonFormsSchemaParser().Parse(NullableObjectSchemaJson);

    [Fact]
    public void Parse_RequiredChildOfAbsentNullableObject_IsNotReportedMissing()
    {
        // The "smtp" section was never toggled on, so it's absent from the document entirely -
        // its required "host" must not block validation.
        const string doc = "{}";

        ConfigDocumentParseResult result = new ConfigDocumentEngine().Parse(
            doc,
            NullableObjectSchema()
        );

        Assert.True(result.IsValid);
        Assert.DoesNotContain("smtp/host", result.MissingRequiredKeys, StringComparer.Ordinal);
    }

    [Fact]
    public void Parse_RequiredChildOfPresentNullableObject_IsStillReportedMissing()
    {
        // Once toggled on, the required child is enforced as normal.
        const string doc = """{ "smtp": {} }""";

        ConfigDocumentParseResult result = new ConfigDocumentEngine().Parse(
            doc,
            NullableObjectSchema()
        );

        Assert.False(result.IsValid);
        Assert.Contains("smtp/host", result.MissingRequiredKeys, StringComparer.Ordinal);
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
