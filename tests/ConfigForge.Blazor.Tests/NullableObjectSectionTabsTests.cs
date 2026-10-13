using System;
using System.Linq;
using Bunit;
using ConfigForge.Abstractions;
using ConfigForge.Blazor.Components;
using ConfigForge.Blazor.Services;
using ConfigForge.Core;
using ConfigForge.Core.Documents;
using ConfigForge.Core.Schema;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ConfigForge.Blazor.Tests;

public sealed class NullableObjectSectionTabsTests : BunitContext
{
    public NullableObjectSectionTabsTests()
    {
        Services.AddLogging();
        Services.AddConfigForgeCore();
        Services.AddConfigForgeBlazor();
    }

    private const string SchemaJson = """
        {
          "schema": {
            "type": "object",
            "properties": {
              "Mapping": {
                "type": "object",
                "x-control": "nullable-object",
                "title": "Mapping",
                "properties": {
                  "Loose": { "type": "string", "title": "Loose" },
                  "Status": {
                    "type": "object",
                    "x-section": "Status",
                    "properties": { "Ok": { "type": "string", "title": "OkField" } }
                  },
                  "Notes": {
                    "type": "object",
                    "x-section": "Notes",
                    "properties": { "Text": { "type": "string", "title": "TextField" } }
                  }
                }
              }
            }
          },
          "x-cf": { "id": "tabs" }
        }
        """;

    private IRenderedComponent<ConfigForgeShell> RenderEnabled()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(SchemaJson);
        ConfigDocument document = new();
        document["Mapping"] = new System.Collections.Generic.Dictionary<string, object?>();

        return Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema).Add(p => p.Document, document)
        );
    }

    private static bool HasLabel(IRenderedComponent<ConfigForgeShell> cut, string text) =>
        cut.FindAll("label")
            .Any(label => label.TextContent.Contains(text, StringComparison.Ordinal));

    [Fact]
    public void SectionedChildren_RenderAsTabs_WithLooseFieldsAbove()
    {
        IRenderedComponent<ConfigForgeShell> cut = RenderEnabled();

        string[] tabs =
        [
            .. cut.FindAll(".cf-nullable-object .cf-tab").Select(t => t.TextContent.Trim()),
        ];
        Assert.Equal(["Status", "Notes"], tabs);
        Assert.True(HasLabel(cut, "Loose"));
        Assert.True(HasLabel(cut, "OkField"));
        Assert.False(HasLabel(cut, "TextField"));
    }

    [Fact]
    public void SelectingATab_ShowsOnlyThatSectionsFields()
    {
        IRenderedComponent<ConfigForgeShell> cut = RenderEnabled();

        cut.FindAll(".cf-nullable-object .cf-tab")[1].Click();

        Assert.True(HasLabel(cut, "TextField"));
        Assert.False(HasLabel(cut, "OkField"));
    }
}
