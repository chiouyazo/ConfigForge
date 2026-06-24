using ConfigForge.Abstractions;
using ConfigForge.Core.Documents;
using ConfigForge.Core.Schema;
using Xunit;

namespace ConfigForge.Core.Tests;

public sealed class DocumentEngineTests
{
    private const string ValidDocJson = """
        {
          "endpoint_url": "https://example.com/api",
          "api_secret": "s3cret",
          "target_channel": "general",
          "interval_minutes": 30,
          "enabled": true
        }
        """;

    private static ConfigSchema Schema() => new JsonFormsSchemaParser().Parse(CanonicalSchema.Json);

    [Fact]
    public void Parse_ValidDocument_IsValidWithNoIssues()
    {
        var engine = new ConfigDocumentEngine();

        ConfigDocumentParseResult result = engine.Parse(ValidDocJson, Schema());

        Assert.True(result.IsValid);
        Assert.Empty(result.MissingRequiredKeys);
        Assert.Empty(result.UnknownKeys);
        Assert.Empty(result.InvalidValues);
        Assert.Null(result.JsonError);
    }

    [Fact]
    public void Parse_MissingRequiredKey_ReportsItAndIsInvalid()
    {
        var engine = new ConfigDocumentEngine();
        const string json = """
            {
              "endpoint_url": "https://example.com/api",
              "target_channel": "general"
            }
            """;

        ConfigDocumentParseResult result = engine.Parse(json, Schema());

        Assert.Contains("api_secret", result.MissingRequiredKeys);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Parse_UnknownKey_ReportedButStillValid()
    {
        var engine = new ConfigDocumentEngine();
        const string json = """
            {
              "endpoint_url": "https://example.com/api",
              "api_secret": "s3cret",
              "foo": 1
            }
            """;

        ConfigDocumentParseResult result = engine.Parse(json, Schema());

        Assert.Contains("foo", result.UnknownKeys);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Parse_ValueExceedingMaximum_ReportedAsInvalid()
    {
        var engine = new ConfigDocumentEngine();
        const string json = """
            {
              "endpoint_url": "https://example.com/api",
              "api_secret": "s3cret",
              "interval_minutes": 5000
            }
            """;

        ConfigDocumentParseResult result = engine.Parse(json, Schema());

        Assert.NotEmpty(result.InvalidValues);
        Assert.Contains(result.InvalidValues, e => e.Key == "interval_minutes");
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Parse_ValueNotMatchingPattern_ReportedAsInvalid()
    {
        var engine = new ConfigDocumentEngine();
        const string json = """
            {
              "endpoint_url": "ftp://example.com",
              "api_secret": "s3cret"
            }
            """;

        ConfigDocumentParseResult result = engine.Parse(json, Schema());

        Assert.Contains(result.InvalidValues, e => e.Key == "endpoint_url");
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Parse_MalformedJson_SetsJsonErrorAndNeverThrows()
    {
        var engine = new ConfigDocumentEngine();

        ConfigDocumentParseResult result = engine.Parse("{not json", Schema());

        Assert.NotNull(result.JsonError);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Serialize_ThenReparse_RoundTripsValuesIncludingUnknownKey()
    {
        var engine = new ConfigDocumentEngine();
        ConfigSchema schema = Schema();
        const string json = """
            {
              "endpoint_url": "https://example.com/api",
              "api_secret": "s3cret",
              "interval_minutes": 30,
              "enabled": false,
              "foo": "bar"
            }
            """;

        ConfigDocument first = engine.Parse(json, schema).Document;
        string serialized = engine.Serialize(first);
        ConfigDocument second = engine.Parse(serialized, schema).Document;

        Assert.Equal("https://example.com/api", second["endpoint_url"]);
        Assert.Equal("s3cret", second["api_secret"]);
        Assert.Equal(30L, second["interval_minutes"]);
        Assert.Equal(false, second["enabled"]);
        Assert.Equal("bar", second["foo"]);
    }

    [Fact]
    public void Diff_ReportsAddedRemovedAndModified()
    {
        var engine = new ConfigDocumentEngine();
        var original = new ConfigDocument
        {
            ["keep"] = "same",
            ["change"] = "old",
            ["remove"] = "gone",
        };
        var modified = new ConfigDocument
        {
            ["keep"] = "same",
            ["change"] = "new",
            ["add"] = "fresh",
        };

        ConfigDiff diff = engine.Diff(original, modified);

        Assert.True(diff.HasChanges);
        Assert.Contains(diff.Changes, c => c.Key == "add" && c.Kind == ConfigChangeKind.Added);
        Assert.Contains(diff.Changes, c => c.Key == "remove" && c.Kind == ConfigChangeKind.Removed);
        Assert.Contains(
            diff.Changes,
            c => c.Key == "change" && c.Kind == ConfigChangeKind.Modified
        );
        Assert.DoesNotContain(diff.Changes, c => c.Key == "keep");
    }

    [Fact]
    public void Diff_IdenticalDocuments_HasNoChanges()
    {
        var engine = new ConfigDocumentEngine();
        var a = new ConfigDocument { ["x"] = 1L, ["y"] = "z" };
        var b = new ConfigDocument { ["x"] = 1L, ["y"] = "z" };

        ConfigDiff diff = engine.Diff(a, b);

        Assert.False(diff.HasChanges);
        Assert.Empty(diff.Changes);
    }
}
