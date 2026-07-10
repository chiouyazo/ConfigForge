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
/// bUnit tests for the root <see cref="ConfigForgeShell"/> component, verifying it
/// renders against an empty schema using the DI-provided DefaultThemeProvider.
/// </summary>
public sealed class ConfigForgeShellTests : BunitContext
{
    public ConfigForgeShellTests()
    {
        Services.AddLogging();
        Services.AddConfigForgeCore();
        Services.AddConfigForgeBlazor();
    }

    [Fact]
    public void ConfigForgeShell_EmptySchema_RendersWithoutThrowing()
    {
        var schema = new ConfigSchema();

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema)
        );

        Assert.Contains("cf-shell", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigForgeShell_ClickingSecondCategory_SwitchesShownFields()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(TwoCategorySchema);

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema)
        );

        Assert.Contains("Endpoint URL", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Interval (minutes)", cut.Markup, StringComparison.Ordinal);

        var categoryButtons = cut.FindAll("button.cf-category-item");
        Assert.Equal(2, categoryButtons.Count);
        categoryButtons[1].Click();

        Assert.Contains("Interval (minutes)", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Endpoint URL", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigForgeShell_CollectionCategory_RendersEntriesAndAddButton()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(CollectionSchema);

        var document = new ConfigDocument();
        document["shops"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["11111111-1111-1111-1111-111111111111"] = new Dictionary<string, object?>(
                StringComparer.Ordinal
            )
            {
                ["type"] = "shopware6",
                ["name"] = "My First Shop",
            },
        };

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema).Add(p => p.Document, document)
        );

        // The collection category is a top-level sidebar item...
        Assert.Contains("cf-collection-list", cut.Markup, StringComparison.Ordinal);
        // ...listing each shop by its name...
        Assert.Contains("My First Shop", cut.Markup, StringComparison.Ordinal);
        // ...plus the add affordance.
        Assert.Contains("cf-collection-add", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Add shop", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigForgeShell_AddCollectionEntry_CreatesAndSelectsIt()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(CollectionSchema);

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema)
        );

        // No entry yet, then open the add dialog, name it, and confirm.
        Assert.DoesNotContain("Freshly Added", cut.Markup, StringComparison.Ordinal);
        cut.Find(".cf-collection-add").Click();
        cut.Find("#cf-add-name").Input("Freshly Added");
        cut.Find(".cf-modal .cf-button-primary").Click();

        // The new entry is listed in the sidebar and its form is shown (the selected entry).
        Assert.Contains("Freshly Added", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("cf-modal-backdrop", cut.Markup, StringComparison.Ordinal);
        var nameInputs = cut.FindAll("input.cf-input[value=\"Freshly Added\"]");
        Assert.NotEmpty(nameInputs);
    }

    [Fact]
    public void ConfigForgeShell_RemoveCollectionEntry_RequiresConfirmThenRemoves()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(CollectionSchema);

        var document = new ConfigDocument();
        document["shops"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["11111111-1111-1111-1111-111111111111"] = new Dictionary<string, object?>(
                StringComparer.Ordinal
            )
            {
                ["type"] = "shopware6",
                ["name"] = "Doomed Shop",
            },
        };

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema).Add(p => p.Document, document)
        );

        Assert.Contains("Doomed Shop", cut.Markup, StringComparison.Ordinal);

        // Removing asks for confirmation first...
        cut.Find(".cf-collection-remove").Click();
        Assert.Contains("cf-modal-backdrop", cut.Markup, StringComparison.Ordinal);
        // ...and the entry is still there until confirmed.
        Assert.Contains("Doomed Shop", cut.Markup, StringComparison.Ordinal);

        cut.Find(".cf-modal .cf-button-danger").Click();
        Assert.DoesNotContain("Doomed Shop", cut.Markup, StringComparison.Ordinal);
    }

    private const string CollectionSchema = """
        {
          "schema": {
            "type": "object",
            "properties": {
              "shops": {
                "type": "object",
                "x-key-format": "uuid",
                "additionalProperties": {
                  "oneOf": [
                    {
                      "type": "object",
                      "properties": {
                        "type": { "type": "string", "const": "shopware6" },
                        "name": { "type": "string", "title": "Name" }
                      }
                    }
                  ]
                }
              }
            }
          },
          "uiSchema": {
            "type": "Categorization",
            "elements": [
              { "type": "Category", "label": "Shop", "elements": [ { "type": "Control", "scope": "#/properties/shops" } ] }
            ]
          },
          "x-cf": {
            "id": "coll", "name": "Collection",
            "categories": { "Shop": { "collection": "shops", "collectionLabel": "name", "collectionAddLabel": "Add shop" } }
          }
        }
        """;

    private const string TwoCategorySchema = """
        {
          "schema": {
            "type": "object",
            "properties": {
              "endpoint_url": { "type": "string", "title": "Endpoint URL" },
              "interval_minutes": { "type": "integer", "title": "Interval (minutes)", "minimum": 1, "maximum": 1440, "default": 60 }
            }
          },
          "uiSchema": {
            "type": "Categorization",
            "elements": [
              { "type": "Category", "label": "Connection", "elements": [ { "type": "Control", "scope": "#/properties/endpoint_url" } ] },
              { "type": "Category", "label": "Synchronisation", "elements": [ { "type": "Control", "scope": "#/properties/interval_minutes" } ] }
            ]
          },
          "x-cf": { "id": "two-cat", "name": "Two Categories" }
        }
        """;
}
