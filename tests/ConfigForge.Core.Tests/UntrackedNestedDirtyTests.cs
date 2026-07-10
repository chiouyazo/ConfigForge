using ConfigForge.Abstractions;
using ConfigForge.Core.Documents;
using ConfigForge.Core.Schema;
using Xunit;

namespace ConfigForge.Core.Tests;

public sealed class UntrackedNestedDirtyTests
{
    private const string Json = """
        {
          "schema": {
            "type": "object",
            "properties": {
              "Diagnostics": {
                "type": "object",
                "properties": {
                  "TestField": { "type": "string" },
                  "Level": { "type": "string" }
                }
              }
            }
          },
          "x-cf": {
            "id": "nested-untracked",
            "controls": { "Diagnostics/TestField": { "tracked": false } }
          }
        }
        """;

    private static ConfigSchema Parse() => new JsonFormsSchemaParser().Parse(Json);

    [Fact]
    public void Parser_MarksNestedFieldUntracked()
    {
        ConfigSchema schema = Parse();
        Assert.False(schema.Fields["Diagnostics/TestField"].Tracked);
        Assert.Contains("Diagnostics/TestField", schema.UntrackedKeys);
    }

    [Fact]
    public void EditingNestedUntrackedField_IntoNewParent_DoesNotMarkDirty()
    {
        ConfigSchema schema = Parse();
        ConfigDocument doc = new();
        DirtyStateTracker tracker = new() { IgnoredKeys = schema.UntrackedKeys };
        tracker.Snapshot(doc);

        // Parent "Diagnostics" did not exist at baseline; editing only the untracked
        // field auto-creates it. This must NOT count as a change.
        doc["Diagnostics/TestField"] = "tester@example.com";
        tracker.Update(doc);

        Assert.False(tracker.IsDirty);
    }

    [Fact]
    public void EditingNestedTrackedSibling_StillMarksDirty()
    {
        ConfigSchema schema = Parse();
        ConfigDocument doc = new();
        DirtyStateTracker tracker = new() { IgnoredKeys = schema.UntrackedKeys };
        tracker.Snapshot(doc);

        doc["Diagnostics/Level"] = "Debug";
        tracker.Update(doc);

        Assert.True(tracker.IsDirty);
    }
}
