using ConfigForge.Abstractions;
using ConfigForge.Core.Documents;
using ConfigForge.Core.Schema;
using Xunit;

namespace ConfigForge.Core.Tests;

public sealed class UntrackedFieldTests
{
    private const string Json = """
        {
          "schema": {
            "type": "object",
            "properties": {
              "Endpoint": { "type": "string" },
              "TestRecipient": { "type": "string" }
            }
          },
          "x-cf": {
            "id": "untracked-test",
            "controls": { "TestRecipient": { "tracked": false } }
          }
        }
        """;

    private static ConfigSchema Parse() => new JsonFormsSchemaParser().Parse(Json);

    [Fact]
    public void Parse_MarksFieldUntracked()
    {
        ConfigSchema schema = Parse();

        Assert.True(schema.Fields["Endpoint"].Tracked);
        Assert.False(schema.Fields["TestRecipient"].Tracked);
        Assert.Contains("TestRecipient", schema.UntrackedKeys);
        Assert.DoesNotContain("Endpoint", schema.UntrackedKeys);
    }

    [Fact]
    public void DirtyTracker_IgnoresUntrackedField()
    {
        ConfigSchema schema = Parse();
        ConfigDocument document = new();
        DirtyStateTracker tracker = new() { IgnoredKeys = schema.UntrackedKeys };
        tracker.Snapshot(document);

        document["TestRecipient"] = "tester@example.com";
        tracker.Update(document);
        Assert.False(tracker.IsDirty);

        document["Endpoint"] = "https://api.example.com";
        tracker.Update(document);
        Assert.True(tracker.IsDirty);
    }

    [Fact]
    public void Serialize_OmitsUntrackedFieldFromSavedOutput()
    {
        ConfigSchema schema = Parse();
        ConfigDocument document = new();
        document["Endpoint"] = "https://api.example.com";
        document["TestRecipient"] = "tester@example.com";

        ConfigDocumentEngine engine = new();
        string saved = engine.Serialize(document, schema);

        Assert.Contains("Endpoint", saved, StringComparison.Ordinal);
        Assert.DoesNotContain("TestRecipient", saved, StringComparison.Ordinal);
        Assert.DoesNotContain("tester@example.com", saved, StringComparison.Ordinal);

        // The plain overload still includes everything (the live document is intact).
        Assert.Contains("TestRecipient", engine.Serialize(document), StringComparison.Ordinal);
    }
}
