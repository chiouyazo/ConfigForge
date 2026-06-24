using Bunit;
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
