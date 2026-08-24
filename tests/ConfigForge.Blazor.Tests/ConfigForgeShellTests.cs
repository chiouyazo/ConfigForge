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
        document["connectors"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["11111111-1111-1111-1111-111111111111"] = new Dictionary<string, object?>(
                StringComparer.Ordinal
            )
            {
                ["type"] = "http",
                ["name"] = "My First Connector",
            },
        };

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema).Add(p => p.Document, document)
        );

        // The collection category is a top-level sidebar item...
        Assert.Contains("cf-collection-list", cut.Markup, StringComparison.Ordinal);
        // ...listing each connector by its name...
        Assert.Contains("My First Connector", cut.Markup, StringComparison.Ordinal);
        // ...plus the add affordance.
        Assert.Contains("cf-collection-add", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Add connector", cut.Markup, StringComparison.Ordinal);
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
        // The label field renders with its own control inside the dialog (not a free "name" box).
        cut.Find(".cf-modal input.cf-input").Change("Freshly Added");
        cut.Find(".cf-modal .cf-button-primary").Click();

        // The new entry is listed in the sidebar and its form is shown (the selected entry).
        Assert.Contains("Freshly Added", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("cf-modal-backdrop", cut.Markup, StringComparison.Ordinal);
        var nameInputs = cut.FindAll("input.cf-input[value=\"Freshly Added\"]");
        Assert.NotEmpty(nameInputs);

        // Regression: the collection is x-key-format=uuid, so the entry key must be a generated
        // GUID, not a sequential "keyN" (which produced "'key1' is not a valid connector id").
        EditingSession session = Services.GetRequiredService<EditingSession>();
        IDictionary<string, object?> connectors = Assert.IsAssignableFrom<
            IDictionary<string, object?>
        >(session.Document["connectors"]);
        string entryKey = Assert.Single(connectors.Keys);
        Assert.True(Guid.TryParse(entryKey, out _), $"expected a GUID key, got '{entryKey}'");
    }

    [Fact]
    public void ConfigForgeShell_AddDialog_RendersLabelFieldControl_AndRevertsOnCancel()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(LabelControlSchema);

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema)
        );
        EditingSession session = Services.GetRequiredService<EditingSession>();

        cut.Find(".cf-collection-add").Click();

        // The entry is staged in place so its label field can render with its own control.
        IDictionary<string, object?> staged = Assert.IsAssignableFrom<IDictionary<string, object?>>(
            session.Document["connectors"]
        );
        Assert.Single(staged.Keys);

        // The label ("mode") is an enum, so the dialog shows its select, not a free-text name box.
        Assert.Empty(cut.FindAll(".cf-modal #cf-add-name"));
        Assert.Contains(
            cut.FindAll(".cf-modal select option"),
            o => string.Equals(o.TextContent.Trim(), "read", StringComparison.Ordinal)
        );

        // Cancelling reverts the staged entry: the collection is restored to its original (absent)
        // state, and nothing is left dirty.
        cut.FindAll(".cf-modal-actions button")
            .Single(b => b.TextContent.Trim() == "Cancel")
            .Click();
        Assert.Null(session.Document["connectors"]);
        Assert.False(session.IsDirty);
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
        document["connectors"] = new Dictionary<string, object?>(StringComparer.Ordinal)
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

        // url is empty → token's [CfEnableWhen(url, IsNotEmpty)] rule (rebased onto connectors/<guid>/url)
        // disables it. This is the case that was silently dead before: rules never fired inside a
        // map entry, so nothing under connectors/<guid>/ could be locked.
        Assert.NotEmpty(cut.FindAll("input[disabled]"));

        EditingSession session = Services.GetRequiredService<EditingSession>();
        session.SetFieldValue("connectors/33333333-3333-3333-3333-333333333333/url", "https://x");

        Assert.Empty(cut.FindAll("input[disabled]"));
    }

    [Fact]
    public void ConfigForgeShell_SectionRuleInOneOf_LocksSubTabUntilConditionMet()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(SectionRuleSchema);

        var document = new ConfigDocument();
        document["connectors"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["44444444-4444-4444-4444-444444444444"] = new Dictionary<string, object?>(
                StringComparer.Ordinal
            )
            {
                ["type"] = "http",
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
        session.SetFieldValue("connectors/44444444-4444-4444-4444-444444444444/url", "https://x");

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
              "connectors": {
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
                        "type": { "type": "string", "const": "http" },
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
              { "type": "Category", "label": "Connectors", "elements": [ { "type": "Control", "scope": "#/properties/connectors" } ] }
            ]
          },
          "x-cf": {
            "id": "secrule", "name": "SecRule",
            "categories": { "Connectors": { "collection": "connectors", "collectionLabel": "url", "collectionAddLabel": "Add connector" } }
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
        document["connectors"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["66666666-6666-6666-6666-666666666666"] = new Dictionary<string, object?>(
                StringComparer.Ordinal
            )
            {
                ["type"] = "http",
                ["name"] = "My First Connector",
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
        Assert.NotEmpty(cut.FindAll("input.cf-input[value=\"My First Connector\"]"));
    }

    [Fact]
    public void ConfigForgeShell_ActiveEntryKey_SelectsThatEntry()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(CollectionSchema);

        var document = new ConfigDocument();
        document["connectors"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"] = new Dictionary<string, object?>(
                StringComparer.Ordinal
            )
            {
                ["type"] = "http",
                ["name"] = "Alpha",
            },
            ["bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"] = new Dictionary<string, object?>(
                StringComparer.Ordinal
            )
            {
                ["type"] = "http",
                ["name"] = "Beta",
            },
        };

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters
                .Add(p => p.Schema, schema)
                .Add(p => p.Document, document)
                .Add(p => p.ActiveCategoryLabel, "Connector")
                .Add(p => p.ActiveEntryKey, "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")
        );

        // The deep-linked entry's form is shown, not the other entry's.
        Assert.NotEmpty(cut.FindAll("input.cf-input[value=\"Beta\"]"));
        Assert.Empty(cut.FindAll("input.cf-input[value=\"Alpha\"]"));
    }

    [Fact]
    public void ConfigForgeShell_SelectingEntry_RaisesOnEntryChanged()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(CollectionSchema);

        var document = new ConfigDocument();
        document["connectors"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["cccccccc-cccc-cccc-cccc-cccccccccccc"] = new Dictionary<string, object?>(
                StringComparer.Ordinal
            )
            {
                ["type"] = "http",
                ["name"] = "Gamma",
            },
        };

        string? raised = null;
        bool got = false;
        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters
                .Add(p => p.Schema, schema)
                .Add(p => p.Document, document)
                .Add(
                    p => p.OnEntryChanged,
                    EventCallback.Factory.Create<string?>(
                        this,
                        k =>
                        {
                            got = true;
                            raised = k;
                        }
                    )
                )
        );

        cut.Find(".cf-collection-select").Click();

        Assert.True(got);
        Assert.Equal("cccccccc-cccc-cccc-cccc-cccccccccccc", raised);
    }

    [Fact]
    public void ConfigForgeShell_SelectingEntry_AwaitsAsyncOnEntryChangedToCompletion()
    {
        // The selection flow must fully await an async OnEntryChanged (the host's writeHash), not
        // fire-and-forget it. This does NOT reproduce the 1.0.15 dispatcher-teardown itself: bUnit's
        // renderer does not enforce Dispatcher-thread affinity, so an off-thread StateHasChanged is
        // swallowed here. That regression is fixed structurally by keeping every await on the
        // Dispatcher (context-dropping awaits are banned; see the CA2007 note in Directory.Build.props).
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(CollectionSchema);

        var document = new ConfigDocument();
        document["connectors"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["dddddddd-dddd-dddd-dddd-dddddddddddd"] = new Dictionary<string, object?>(
                StringComparer.Ordinal
            )
            {
                ["type"] = "http",
                ["name"] = "Delta",
            },
        };

        bool continuationRan = false;
        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters
                .Add(p => p.Schema, schema)
                .Add(p => p.Document, document)
                .Add(
                    p => p.OnEntryChanged,
                    EventCallback.Factory.Create<string?>(
                        this,
                        async (string? _) =>
                        {
                            await Task.Delay(20);
                            continuationRan = true;
                        }
                    )
                )
        );

        cut.Find(".cf-collection-select").Click();

        EditingSession session = Services.GetRequiredService<EditingSession>();
        cut.WaitForAssertion(() =>
        {
            Assert.True(continuationRan);
            Assert.Equal(
                "dddddddd-dddd-dddd-dddd-dddddddddddd",
                session.GetSelectedEntry("connectors")
            );
        });
    }

    [Fact]
    public void ConfigForgeShell_NullableObjectToggledOff_RemovesKeyAndStaysValid()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(NullableObjectSchema);

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema)
        );

        // Enable the optional block, fill nothing, then disable it again.
        cut.Find(".cf-nullable-object input[type=checkbox]").Change(true);
        cut.Find(".cf-nullable-object input[type=checkbox]").Change(false);

        EditingSession session = Services.GetRequiredService<EditingSession>();

        // Off means absent, not a stored null, so the document stays valid (no "should be object").
        Assert.False(session.Document.ContainsKey("alerting"));
        Assert.True(session.ParseResult!.IsValid);
    }

    [Fact]
    public void ConfigForgeShell_SectionScopedAction_InGroupedCategory_MatchesActiveTab()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(GroupedSectionActionSchema);

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema)
        );

        // The default sub-tab is "Logging", so its scoped action shows.
        Assert.Contains("Test log db", cut.Markup, StringComparison.Ordinal);

        // On the "Telemetry" sub-tab it is hidden.
        cut.FindAll(".cf-tab").Single(t => t.TextContent.Trim() == "Telemetry").Click();
        Assert.DoesNotContain("Test log db", cut.Markup, StringComparison.Ordinal);

        // Back on "Logging" it returns.
        cut.FindAll(".cf-tab").Single(t => t.TextContent.Trim() == "Logging").Click();
        Assert.Contains("Test log db", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigForgeShell_CollectionEntryStatus_RendersDotAndDimsInactive()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(CollectionWithStatusSchema);

        var document = new ConfigDocument();
        document["connectors"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["11111111-1111-1111-1111-111111111111"] = new Dictionary<string, object?>(
                StringComparer.Ordinal
            )
            {
                ["type"] = "http",
                ["name"] = "On",
                ["active"] = true,
            },
            ["22222222-2222-2222-2222-222222222222"] = new Dictionary<string, object?>(
                StringComparer.Ordinal
            )
            {
                ["type"] = "http",
                ["name"] = "Off",
                ["active"] = false,
            },
        };

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema).Add(p => p.Document, document)
        );

        Assert.Equal(2, cut.FindAll(".cf-collection-status").Count);
        Assert.Single(cut.FindAll(".cf-collection-status.cf-on"));
        Assert.Single(cut.FindAll(".cf-collection-status.cf-off"));
        Assert.Single(cut.FindAll(".cf-collection-item.cf-inactive"));
    }

    [Fact]
    public void ConfigForgeShell_RequiresEntryAction_HiddenUntilEntrySelected()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(RequiresEntryActionSchema);

        var document = new ConfigDocument();
        document["connectors"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["77777777-7777-7777-7777-777777777777"] = new Dictionary<string, object?>(
                StringComparer.Ordinal
            )
            {
                ["type"] = "http",
                ["name"] = "Connector A",
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
    public void ConfigForgeShell_SectionScopedAction_VisibleOnlyOnItsSubTab()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(SectionActionSchema);

        var document = new ConfigDocument();
        document["connectors"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["88888888-8888-8888-8888-888888888888"] = new Dictionary<string, object?>(
                StringComparer.Ordinal
            )
            {
                ["type"] = "http",
                ["name"] = "A",
            },
        };

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema).Add(p => p.Document, document)
        );

        cut.Find(".cf-collection-select").Click();

        // The default sub-tab is "General", so the Advanced-scoped action is hidden.
        Assert.DoesNotContain("Tune", cut.Markup, StringComparison.Ordinal);

        // Switching to the "Advanced" sub-tab reveals it.
        cut.FindAll(".cf-tab").Single(t => t.TextContent.Trim() == "Advanced").Click();
        Assert.Contains("Tune", cut.Markup, StringComparison.Ordinal);
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

        // Add a connector but leave the required endpoints empty, then try to save.
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
        cut.Find(".cf-modal input.cf-input").Change("Saved Connector");
        cut.Find(".cf-modal .cf-button-primary").Click();
        cut.Find(".cf-save-bar .cf-button-primary").Click();
        Assert.Equal(1, saved);

        EditingSession session = Services.GetRequiredService<EditingSession>();
        string guid = ((IDictionary<string, object?>)session.Document["connectors"]!).Keys.First();
        session.SetFieldValue($"connectors/{guid}/name", "Edited");

        cut.FindAll(".cf-save-bar button")
            .Single(b => b.TextContent.Contains("Discard", StringComparison.Ordinal))
            .Click();
        cut.Find(".cf-modal .cf-button-danger").Click();

        // Discard reverts to the saved state, not the page-load snapshot: the connector must survive.
        IDictionary<string, object?> connectors =
            (IDictionary<string, object?>)session.Document["connectors"]!;
        Assert.Single(connectors.Keys);
        Assert.Equal("Saved Connector", session.Document[$"connectors/{guid}/name"]);
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

        // A freshly added connector must not flag errors the user could not yet have caused.
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
        string guid = ((IDictionary<string, object?>)session.Document["connectors"]!).Keys.First();
        session.SetFieldValue($"connectors/{guid}/endpoints", new List<object?> { "channel" });

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
              "connectors": {
                "type": "object",
                "x-key-format": "uuid",
                "additionalProperties": {
                  "oneOf": [
                    { "type": "object", "properties": {
                      "type": { "type": "string", "const": "http" },
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
              { "type": "Category", "label": "Connectors", "elements": [ { "type": "Control", "scope": "#/properties/connectors" } ] }
            ]
          },
          "x-cf": {
            "id": "reqentry", "name": "ReqEntry",
            "categories": { "Connectors": { "collection": "connectors", "collectionLabel": "name", "collectionAddLabel": "Add connector" } },
            "actions": [
              { "actionId": "connector.test", "label": "Test connection", "requiresEntry": true, "placement": { "category": "Connectors" } }
            ]
          }
        }
        """;

    private const string SectionActionSchema = """
        {
          "schema": {
            "type": "object",
            "properties": {
              "connectors": {
                "type": "object",
                "x-key-format": "uuid",
                "additionalProperties": {
                  "oneOf": [
                    { "type": "object", "properties": {
                      "type": { "type": "string", "const": "http" },
                      "name": { "type": "string", "title": "Name", "x-section": "General" },
                      "timeout": { "type": "integer", "title": "Timeout", "x-section": "Advanced" }
                    } }
                  ]
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
            "id": "secact", "name": "SecAct",
            "categories": { "Connectors": { "collection": "connectors", "collectionLabel": "name", "collectionAddLabel": "Add connector" } },
            "actions": [
              { "actionId": "connector.tune", "label": "Tune", "requiresEntry": true, "placement": { "category": "Connectors", "section": "Advanced" } }
            ]
          }
        }
        """;

    private const string RequiredEntrySchema = """
        {
          "schema": {
            "type": "object",
            "properties": {
              "connectors": {
                "type": "object",
                "x-key-format": "uuid",
                "additionalProperties": {
                  "oneOf": [
                    { "type": "object", "required": ["endpoints"], "properties": {
                      "type": { "type": "string", "const": "http" },
                      "endpoints": { "type": "array", "items": { "type": "string" }, "title": "Sync channels" }
                    } }
                  ]
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
            "id": "reqsave", "name": "ReqSave",
            "categories": { "Connectors": { "collection": "connectors", "collectionLabel": "endpoints", "collectionAddLabel": "Add connector" } }
          }
        }
        """;

    private const string EntryFieldRuleSchema = """
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
              { "type": "Category", "label": "Connectors", "elements": [ { "type": "Control", "scope": "#/properties/connectors" } ] }
            ]
          },
          "x-cf": {
            "id": "entryrule", "name": "EntryRule",
            "categories": { "Connectors": { "collection": "connectors", "collectionLabel": "url", "collectionAddLabel": "Add connector" } }
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
        document["connectors"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["11111111-1111-1111-1111-111111111111"] = new Dictionary<string, object?>(
                StringComparer.Ordinal
            )
            {
                ["type"] = "http",
                ["name"] = "Doomed Connector",
            },
        };

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema).Add(p => p.Document, document)
        );

        Assert.Contains("Doomed Connector", cut.Markup, StringComparison.Ordinal);

        // Removing asks for confirmation first...
        cut.Find(".cf-collection-remove").Click();
        Assert.Contains("cf-modal-backdrop", cut.Markup, StringComparison.Ordinal);
        // ...and the entry is still there until confirmed.
        Assert.Contains("Doomed Connector", cut.Markup, StringComparison.Ordinal);

        cut.Find(".cf-modal .cf-button-danger").Click();
        Assert.DoesNotContain("Doomed Connector", cut.Markup, StringComparison.Ordinal);
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
                  "oneOf": [
                    {
                      "type": "object",
                      "properties": {
                        "type": { "type": "string", "const": "http" },
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
              { "type": "Category", "label": "Connector", "elements": [ { "type": "Control", "scope": "#/properties/connectors" } ] }
            ]
          },
          "x-cf": {
            "id": "coll", "name": "Collection",
            "categories": { "Connector": { "collection": "connectors", "collectionLabel": "name", "collectionAddLabel": "Add connector" } }
          }
        }
        """;

    private const string CollectionWithStatusSchema = """
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
                      "properties": {
                        "type": { "type": "string", "const": "http" },
                        "name": { "type": "string", "title": "Name" },
                        "active": { "type": "boolean", "title": "Active" }
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
              { "type": "Category", "label": "Connectors", "elements": [ { "type": "Control", "scope": "#/properties/connectors" } ] }
            ]
          },
          "x-cf": {
            "id": "status", "name": "Status",
            "categories": { "Connectors": { "collection": "connectors", "collectionLabel": "name", "collectionAddLabel": "Add connector", "collectionEntryStatus": "active" } }
          }
        }
        """;

    private const string LabelControlSchema = """
        {
          "schema": {
            "type": "object",
            "properties": {
              "connectors": {
                "type": "object",
                "x-key-format": "uuid",
                "additionalProperties": {
                  "oneOf": [
                    { "type": "object", "properties": {
                      "type": { "type": "string", "const": "http" },
                      "mode": { "type": "string", "title": "Mode", "enum": ["read", "write"] }
                    } }
                  ]
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
            "id": "labelctl", "name": "LabelCtl",
            "categories": { "Connectors": { "collection": "connectors", "collectionLabel": "mode", "collectionAddLabel": "Add connector" } }
          }
        }
        """;

    private const string NullableObjectSchema = """
        {
          "schema": {
            "type": "object",
            "properties": {
              "alerting": {
                "type": "object",
                "x-control": "nullable-object",
                "properties": { "email": { "type": "string", "title": "Email" } }
              }
            }
          },
          "uiSchema": {
            "type": "Categorization",
            "elements": [
              { "type": "Category", "label": "General", "elements": [ { "type": "Control", "scope": "#/properties/alerting" } ] }
            ]
          },
          "x-cf": { "id": "nul", "name": "Nul" }
        }
        """;

    private const string GroupedSectionActionSchema = """
        {
          "schema": {
            "type": "object",
            "properties": {
              "logLevel": { "type": "string", "title": "Log level" },
              "telemetryToken": { "type": "string", "title": "Telemetry token" }
            }
          },
          "uiSchema": {
            "type": "Categorization",
            "elements": [
              { "type": "Category", "label": "Analytics", "elements": [
                { "type": "Categorization", "elements": [
                  { "type": "Category", "label": "Logging", "elements": [ { "type": "Control", "scope": "#/properties/logLevel" } ] },
                  { "type": "Category", "label": "Telemetry", "elements": [ { "type": "Control", "scope": "#/properties/telemetryToken" } ] }
                ] }
              ] }
            ]
          },
          "x-cf": {
            "id": "grpact", "name": "GrpAct",
            "actions": [
              { "actionId": "log.test", "label": "Test log db", "placement": { "category": "Analytics", "section": "Logging" } }
            ]
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
