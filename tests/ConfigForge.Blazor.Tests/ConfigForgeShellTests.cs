using Bunit;
using ConfigForge.Abstractions;
using ConfigForge.Blazor.Components;
using ConfigForge.Blazor.Services;
using ConfigForge.Core;
using ConfigForge.Core.Schema;
using Microsoft.AspNetCore.Components;
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
    public void ConfigForgeShell_RendersPoweredByLink()
    {
        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, new ConfigSchema())
        );

        var link = cut.Find("a.cf-powered-by");
        Assert.Contains("powered by ConfigForge", link.TextContent, StringComparison.Ordinal);
        Assert.Equal("https://github.com/chiouyazo/ConfigForge", link.GetAttribute("href"));
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

        // Regression: the collection is x-key-format=uuid, so the entry key must be a generated
        // GUID, not a sequential "keyN" (which produced "'key1' is not a valid shop id").
        EditingSession session = Services.GetRequiredService<EditingSession>();
        IDictionary<string, object?> shops = Assert.IsAssignableFrom<IDictionary<string, object?>>(
            session.Document["shops"]
        );
        string entryKey = Assert.Single(shops.Keys);
        Assert.True(Guid.TryParse(entryKey, out _), $"expected a GUID key, got '{entryKey}'");
    }

    [Fact]
    public void ConfigForgeShell_CollectionWithPlainObjectValue_RendersEntryFields()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(ObjectCollectionSchema);

        var document = new ConfigDocument();
        document["schedules"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["22222222-2222-2222-2222-222222222222"] = new Dictionary<string, object?>(
                StringComparer.Ordinal
            )
            {
                ["label"] = "Nightly",
            },
        };

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema).Add(p => p.Document, document)
        );

        // Selecting the entry must expand the plain-object value into its child fields, not render
        // the whole object raw ("Unsupported control type 'object'").
        cut.Find(".cf-collection-select").Click();
        Assert.DoesNotContain("Unsupported control type", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Interval (s)", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigForgeShell_CategoryEnableRule_LocksTabUntilConditionMet()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(GatedCategorySchema);

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema)
        );

        // connectionValid is unset → the "Config" tab is rendered but locked (disabled).
        var configButton = cut.FindAll("button.cf-category-item")
            .Single(b => b.TextContent.Contains("Config", StringComparison.Ordinal));
        Assert.Contains("cf-disabled", configButton.ClassName, StringComparison.Ordinal);
        Assert.True(configButton.HasAttribute("disabled"));

        // Set the watched field → the tab unlocks live (no reload).
        EditingSession session = Services.GetRequiredService<EditingSession>();
        session.SetFieldValue("connectionValid", "ok");

        configButton = cut.FindAll("button.cf-category-item")
            .Single(b => b.TextContent.Contains("Config", StringComparison.Ordinal));
        Assert.DoesNotContain("cf-disabled", configButton.ClassName, StringComparison.Ordinal);
        Assert.False(configButton.HasAttribute("disabled"));
    }

    private const string GatedCategorySchema = """
        {
          "schema": {
            "type": "object",
            "properties": {
              "connectionValid": { "type": "string", "title": "Connection Valid" },
              "threshold": { "type": "string", "title": "Threshold" }
            }
          },
          "uiSchema": {
            "type": "Categorization",
            "elements": [
              { "type": "Category", "label": "Connection", "elements": [ { "type": "Control", "scope": "#/properties/connectionValid" } ] },
              { "type": "Category", "label": "Config", "elements": [ { "type": "Control", "scope": "#/properties/threshold" } ] }
            ]
          },
          "x-cf": {
            "id": "gated", "name": "Gated",
            "categories": {
              "Config": {
                "rule": {
                  "effect": "ENABLE",
                  "condition": { "scope": "#/properties/connectionValid", "schema": { "not": { "type": "null" } } }
                }
              }
            }
          }
        }
        """;

    [Fact]
    public void ConfigForgeShell_FieldRuleInsideMapEntry_DisablesUntilSiblingSet()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(EntryFieldRuleSchema);

        var document = new ConfigDocument();
        document["shops"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["33333333-3333-3333-3333-333333333333"] = new Dictionary<string, object?>(
                StringComparer.Ordinal
            )
            {
                ["url"] = "",
                ["token"] = "abc",
            },
        };

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema).Add(p => p.Document, document)
        );
        cut.Find(".cf-collection-select").Click();

        // url is empty → token's [CfEnableWhen(url, IsNotEmpty)] rule (rebased onto shops/<guid>/url)
        // disables it. This is the case that was silently dead before: rules never fired inside a
        // map entry, so nothing under shops/<guid>/ could be locked.
        Assert.NotEmpty(cut.FindAll("input[disabled]"));

        EditingSession session = Services.GetRequiredService<EditingSession>();
        session.SetFieldValue("shops/33333333-3333-3333-3333-333333333333/url", "https://x");

        Assert.Empty(cut.FindAll("input[disabled]"));
    }

    [Fact]
    public void ConfigForgeShell_SectionRuleInOneOf_LocksSubTabUntilConditionMet()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(SectionRuleSchema);

        var document = new ConfigDocument();
        document["shops"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["44444444-4444-4444-4444-444444444444"] = new Dictionary<string, object?>(
                StringComparer.Ordinal
            )
            {
                ["type"] = "shopware6",
                ["url"] = "",
            },
        };

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema).Add(p => p.Document, document)
        );
        cut.Find(".cf-collection-select").Click();

        // url is empty → the "Config" sub-tab is rendered but locked (disabled).
        var configTab = cut.FindAll("button.cf-tab")
            .Single(b => b.TextContent.Contains("Config", StringComparison.Ordinal));
        Assert.Contains("cf-disabled", configTab.ClassName, StringComparison.Ordinal);
        Assert.True(configTab.HasAttribute("disabled"));

        // Fill the entry's url → the sub-tab unlocks live.
        EditingSession session = Services.GetRequiredService<EditingSession>();
        session.SetFieldValue("shops/44444444-4444-4444-4444-444444444444/url", "https://x");

        configTab = cut.FindAll("button.cf-tab")
            .Single(b => b.TextContent.Contains("Config", StringComparison.Ordinal));
        Assert.DoesNotContain("cf-disabled", configTab.ClassName, StringComparison.Ordinal);
        Assert.False(configTab.HasAttribute("disabled"));
    }

    private const string SectionRuleSchema = """
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
                      "x-section-rules": {
                        "Config": {
                          "effect": "ENABLE",
                          "condition": { "scope": "#/properties/url", "schema": { "not": { "enum": [null, ""] } } }
                        }
                      },
                      "properties": {
                        "type": { "type": "string", "const": "shopware6" },
                        "url": { "type": "string", "title": "Url", "x-section": "General" },
                        "threshold": { "type": "string", "title": "Threshold", "x-section": "Config" }
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
              { "type": "Category", "label": "Shops", "elements": [ { "type": "Control", "scope": "#/properties/shops" } ] }
            ]
          },
          "x-cf": {
            "id": "secrule", "name": "SecRule",
            "categories": { "Shops": { "collection": "shops", "collectionLabel": "url", "collectionAddLabel": "Add shop" } }
          }
        }
        """;

    [Fact]
    public void ConfigForgeShell_HiddenField_IsNotRendered()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(HiddenFieldSchema);

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema)
        );

        Assert.Contains("Visible Field", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Secret State", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigForgeShell_ClickingCollectionCategory_SelectsFirstEntry()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(CollectionSchema);

        var document = new ConfigDocument();
        document["shops"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["66666666-6666-6666-6666-666666666666"] = new Dictionary<string, object?>(
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

        // Nothing selected yet: the collection category is just the placeholder dead end.
        Assert.Contains("Select an entry", cut.Markup, StringComparison.Ordinal);

        // Clicking the category header selects the first entry so the click lands on a real form.
        cut.Find("button.cf-category-item").Click();
        Assert.DoesNotContain("Select an entry", cut.Markup, StringComparison.Ordinal);
        Assert.NotEmpty(cut.FindAll("input.cf-input[value=\"My First Shop\"]"));
    }

    [Fact]
    public void ConfigForgeShell_RequiresEntryAction_HiddenUntilEntrySelected()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(RequiresEntryActionSchema);

        var document = new ConfigDocument();
        document["shops"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["77777777-7777-7777-7777-777777777777"] = new Dictionary<string, object?>(
                StringComparer.Ordinal
            )
            {
                ["type"] = "shopware6",
                ["name"] = "Shop A",
            },
        };

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema).Add(p => p.Document, document)
        );

        // No entry selected yet → the per-entry "Test connection" action is not shown.
        Assert.DoesNotContain("Test connection", cut.Markup, StringComparison.Ordinal);

        cut.Find(".cf-collection-select").Click();
        Assert.Contains("Test connection", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigForgeShell_SaveWithMissingRequiredInEntry_IsBlocked()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(RequiredEntrySchema);

        bool saved = false;
        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters
                .Add(p => p.Schema, schema)
                .Add(
                    p => p.OnSave,
                    EventCallback.Factory.Create<ConfigDocument>(this, () => saved = true)
                )
        );

        // Add a shop but leave the required syncChannels empty, then try to save.
        cut.Find(".cf-collection-add").Click();
        cut.Find(".cf-modal .cf-button-primary").Click();
        cut.Find(".cf-save-bar .cf-button-primary").Click();

        // The save is blocked (the invalid document never reaches the consumer) and the required
        // field is surfaced instead of a raw deserialization crash downstream.
        Assert.False(saved);
        Assert.Contains("This field is required", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigForgeShell_DiscardAfterSave_KeepsSavedData()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(CollectionSchema);

        int saved = 0;
        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters
                .Add(p => p.Schema, schema)
                .Add(
                    p => p.OnSave,
                    EventCallback.Factory.Create<ConfigDocument>(this, () => saved++)
                )
        );

        cut.Find(".cf-collection-add").Click();
        cut.Find("#cf-add-name").Input("Saved Shop");
        cut.Find(".cf-modal .cf-button-primary").Click();
        cut.Find(".cf-save-bar .cf-button-primary").Click();
        Assert.Equal(1, saved);

        EditingSession session = Services.GetRequiredService<EditingSession>();
        string guid = ((IDictionary<string, object?>)session.Document["shops"]!).Keys.First();
        session.SetFieldValue($"shops/{guid}/name", "Edited");

        cut.FindAll(".cf-save-bar button")
            .Single(b => b.TextContent.Contains("Discard", StringComparison.Ordinal))
            .Click();
        cut.Find(".cf-modal .cf-button-danger").Click();

        // Discard reverts to the saved state, not the page-load snapshot: the shop must survive.
        IDictionary<string, object?> shops =
            (IDictionary<string, object?>)session.Document["shops"]!;
        Assert.Single(shops.Keys);
        Assert.Equal("Saved Shop", session.Document[$"shops/{guid}/name"]);
    }

    [Fact]
    public void ConfigForgeShell_NewEntry_ShowsNoErrorsUntilTouchedOrSave()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(RequiredEntrySchema);

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters
                .Add(p => p.Schema, schema)
                .Add(p => p.OnSave, EventCallback.Factory.Create<ConfigDocument>(this, () => { }))
        );

        cut.Find(".cf-collection-add").Click();
        cut.Find(".cf-modal .cf-button-primary").Click();

        // A freshly added shop must not flag errors the user could not yet have caused.
        Assert.DoesNotContain("This field is required", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "This configuration is not valid",
            cut.Markup,
            StringComparison.Ordinal
        );
        Assert.False(cut.Find(".cf-save-bar .cf-button-primary").HasAttribute("disabled"));
    }

    [Fact]
    public void ConfigForgeShell_SaveThenFix_RevealsThenClearsError()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(RequiredEntrySchema);

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters
                .Add(p => p.Schema, schema)
                .Add(p => p.OnSave, EventCallback.Factory.Create<ConfigDocument>(this, () => { }))
        );

        cut.Find(".cf-collection-add").Click();
        cut.Find(".cf-modal .cf-button-primary").Click();

        // Pressing save reveals what is missing and disables the button.
        cut.Find(".cf-save-bar .cf-button-primary").Click();
        Assert.Contains("This field is required", cut.Markup, StringComparison.Ordinal);
        Assert.True(cut.Find(".cf-save-bar .cf-button-primary").HasAttribute("disabled"));

        // Filling it clears the error live and re-enables save.
        EditingSession session = Services.GetRequiredService<EditingSession>();
        string guid = ((IDictionary<string, object?>)session.Document["shops"]!).Keys.First();
        session.SetFieldValue($"shops/{guid}/syncChannels", new List<object?> { "channel" });

        Assert.DoesNotContain("This field is required", cut.Markup, StringComparison.Ordinal);
        Assert.False(cut.Find(".cf-save-bar .cf-button-primary").HasAttribute("disabled"));
    }

    private const string HiddenFieldSchema = """
        {
          "schema": {
            "type": "object",
            "properties": {
              "visibleField": { "type": "string", "title": "Visible Field" },
              "secretState": { "type": "string", "title": "Secret State", "x-hidden": true }
            }
          },
          "x-cf": { "id": "hidden", "name": "Hidden" }
        }
        """;

    private const string RequiresEntryActionSchema = """
        {
          "schema": {
            "type": "object",
            "properties": {
              "shops": {
                "type": "object",
                "x-key-format": "uuid",
                "additionalProperties": {
                  "oneOf": [
                    { "type": "object", "properties": {
                      "type": { "type": "string", "const": "shopware6" },
                      "name": { "type": "string", "title": "Name" }
                    } }
                  ]
                }
              }
            }
          },
          "uiSchema": {
            "type": "Categorization",
            "elements": [
              { "type": "Category", "label": "Shops", "elements": [ { "type": "Control", "scope": "#/properties/shops" } ] }
            ]
          },
          "x-cf": {
            "id": "reqentry", "name": "ReqEntry",
            "categories": { "Shops": { "collection": "shops", "collectionLabel": "name", "collectionAddLabel": "Add shop" } },
            "actions": [
              { "actionId": "shop.test", "label": "Test connection", "requiresEntry": true, "placement": { "category": "Shops" } }
            ]
          }
        }
        """;

    private const string RequiredEntrySchema = """
        {
          "schema": {
            "type": "object",
            "properties": {
              "shops": {
                "type": "object",
                "x-key-format": "uuid",
                "additionalProperties": {
                  "oneOf": [
                    { "type": "object", "required": ["syncChannels"], "properties": {
                      "type": { "type": "string", "const": "shopware6" },
                      "syncChannels": { "type": "array", "items": { "type": "string" }, "title": "Sync channels" }
                    } }
                  ]
                }
              }
            }
          },
          "uiSchema": {
            "type": "Categorization",
            "elements": [
              { "type": "Category", "label": "Shops", "elements": [ { "type": "Control", "scope": "#/properties/shops" } ] }
            ]
          },
          "x-cf": {
            "id": "reqsave", "name": "ReqSave",
            "categories": { "Shops": { "collection": "shops", "collectionLabel": "syncChannels", "collectionAddLabel": "Add shop" } }
          }
        }
        """;

    private const string EntryFieldRuleSchema = """
        {
          "schema": {
            "type": "object",
            "properties": {
              "shops": {
                "type": "object",
                "x-key-format": "uuid",
                "additionalProperties": {
                  "type": "object",
                  "properties": {
                    "url": { "type": "string", "title": "Url" },
                    "token": {
                      "type": "string",
                      "title": "Token",
                      "x-rule": {
                        "effect": "ENABLE",
                        "condition": { "scope": "#/properties/url", "schema": { "not": { "enum": [null, ""] } } }
                      }
                    }
                  }
                }
              }
            }
          },
          "uiSchema": {
            "type": "Categorization",
            "elements": [
              { "type": "Category", "label": "Shops", "elements": [ { "type": "Control", "scope": "#/properties/shops" } ] }
            ]
          },
          "x-cf": {
            "id": "entryrule", "name": "EntryRule",
            "categories": { "Shops": { "collection": "shops", "collectionLabel": "url", "collectionAddLabel": "Add shop" } }
          }
        }
        """;

    private const string ObjectCollectionSchema = """
        {
          "schema": {
            "type": "object",
            "properties": {
              "schedules": {
                "type": "object",
                "x-key-format": "uuid",
                "additionalProperties": {
                  "type": "object",
                  "properties": {
                    "label": { "type": "string", "title": "Label" },
                    "intervalSeconds": { "type": "integer", "title": "Interval (s)" }
                  }
                }
              }
            }
          },
          "uiSchema": {
            "type": "Categorization",
            "elements": [
              { "type": "Category", "label": "Schedules", "elements": [ { "type": "Control", "scope": "#/properties/schedules" } ] }
            ]
          },
          "x-cf": {
            "id": "objcoll", "name": "ObjColl",
            "categories": { "Schedules": { "collection": "schedules", "collectionLabel": "label", "collectionAddLabel": "Add schedule" } }
          }
        }
        """;

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
