using Bunit;
using ConfigForge.Abstractions;
using ConfigForge.Blazor.Components;
using ConfigForge.Blazor.Services;
using ConfigForge.Core;
using ConfigForge.Core.Schema;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ConfigForge.Blazor.Tests;

/// <summary>
/// bUnit tests for a collection category backed by a plugin-registered collection loader
/// (<see cref="IPluginRegistry.RegisterCollectionLoader"/>): it must render the live loader
/// entries instead of the document's, refresh fresh on demand, stay read-only, and still
/// support a <c>RequiresEntry</c> action against the selected row.
/// </summary>
public sealed class CollectionLoaderTests : BunitContext
{
    public CollectionLoaderTests()
    {
        Services.AddLogging();
        Services.AddConfigForgeCore();
        Services.AddConfigForgeBlazor();
    }

    [Fact]
    public void CollectionCategory_WithRegisteredLoader_RendersLoaderEntriesNotDocument()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(CollectionSchema);
        IPluginRegistry registry = Services.GetRequiredService<IPluginRegistry>();
        registry.RegisterCollectionLoader(
            "connectors",
            (_, _) =>
                Task.FromResult<IReadOnlyList<ConfigDocument>>([
                    new ConfigDocument(new Dictionary<string, object?> { ["name"] = "Live Row" }),
                ])
        );

        var document = new ConfigDocument();
        document["connectors"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["11111111-1111-1111-1111-111111111111"] = new Dictionary<string, object?>(
                StringComparer.Ordinal
            )
            {
                ["name"] = "Document Row",
            },
        };

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema).Add(p => p.Document, document)
        );

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Live Row", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("Document Row", cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void CollectionCategory_WithRegisteredLoader_RendersReadOnly_NoAddOrRemove()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(CollectionSchema);
        IPluginRegistry registry = Services.GetRequiredService<IPluginRegistry>();
        registry.RegisterCollectionLoader(
            "connectors",
            (_, _) =>
                Task.FromResult<IReadOnlyList<ConfigDocument>>([
                    new ConfigDocument(new Dictionary<string, object?> { ["name"] = "Row A" }),
                ])
        );

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema)
        );

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Row A", cut.Markup, StringComparison.Ordinal);
            Assert.Empty(cut.FindAll(".cf-collection-add"));
            Assert.Empty(cut.FindAll(".cf-collection-remove"));
            Assert.NotEmpty(cut.FindAll(".cf-collection-refresh"));
            Assert.Empty(cut.FindAll("input.cf-input"));
        });
    }

    [Fact]
    public void RefreshButton_ReInvokesLoader_NotCached()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(CollectionSchema);
        IPluginRegistry registry = Services.GetRequiredService<IPluginRegistry>();

        int callCount = 0;
        registry.RegisterCollectionLoader(
            "connectors",
            (_, _) =>
            {
                callCount++;
                return Task.FromResult<IReadOnlyList<ConfigDocument>>([
                    new ConfigDocument(
                        new Dictionary<string, object?> { ["name"] = $"Row {callCount}" }
                    ),
                ]);
            }
        );

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema)
        );

        cut.WaitForAssertion(() => Assert.Contains("Row 1", cut.Markup, StringComparison.Ordinal));

        cut.Find(".cf-collection-refresh").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.True(callCount >= 2, $"expected the loader to run again, ran {callCount} times");
            Assert.Contains($"Row {callCount}", cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task RequiresEntryAction_AgainstLoaderBackedRow_ReadsItsFieldValues()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(RequiresEntryLoaderSchema);
        IPluginRegistry registry = Services.GetRequiredService<IPluginRegistry>();
        registry.RegisterCollectionLoader(
            "connectors",
            (_, _) =>
                Task.FromResult<IReadOnlyList<ConfigDocument>>([
                    new ConfigDocument(new Dictionary<string, object?> { ["name"] = "Row X" }),
                ])
        );

        string? seenName = null;
        registry.RegisterAction(
            "connector.test",
            context =>
            {
                seenName = context[$"{context.CurrentFieldKey}/name"];
                return Task.CompletedTask;
            }
        );

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema)
        );

        cut.WaitForAssertion(() =>
            Assert.Contains("Test connection", cut.Markup, StringComparison.Ordinal)
        );

        await cut.InvokeAsync(() => cut.Find(".cf-action-bar button").Click());

        Assert.Equal("Row X", seenName);
    }

    private const string CollectionSchema = """
        {
          "schema": {
            "type": "object",
            "properties": {
              "connectors": {
                "type": "object",
                "x-key-format": "uuid",
                "additionalProperties": {
                  "type": "object",
                  "properties": {
                    "name": { "type": "string", "title": "Name" }
                  }
                }
              }
            }
          },
          "uiSchema": {
            "type": "Categorization",
            "elements": [
              { "type": "Category", "label": "Connectors", "elements": [ { "type": "Control", "scope": "#/properties/connectors" } ] }
            ]
          },
          "x-cf": {
            "id": "loadercoll", "name": "LoaderColl",
            "categories": { "Connectors": { "collection": "connectors", "collectionLabel": "name", "collectionAddLabel": "Add connector" } }
          }
        }
        """;

    private const string RequiresEntryLoaderSchema = """
        {
          "schema": {
            "type": "object",
            "properties": {
              "connectors": {
                "type": "object",
                "x-key-format": "uuid",
                "additionalProperties": {
                  "type": "object",
                  "properties": {
                    "name": { "type": "string", "title": "Name" }
                  }
                }
              }
            }
          },
          "uiSchema": {
            "type": "Categorization",
            "elements": [
              { "type": "Category", "label": "Connectors", "elements": [ { "type": "Control", "scope": "#/properties/connectors" } ] }
            ]
          },
          "x-cf": {
            "id": "loaderreq", "name": "LoaderReq",
            "categories": { "Connectors": { "collection": "connectors", "collectionLabel": "name", "collectionAddLabel": "Add connector" } },
            "actions": [
              { "actionId": "connector.test", "label": "Test connection", "requiresEntry": true, "placement": { "category": "Connectors" } }
            ]
          }
        }
        """;
}
