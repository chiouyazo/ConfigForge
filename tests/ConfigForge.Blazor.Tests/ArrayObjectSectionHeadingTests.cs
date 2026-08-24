using Bunit;
using ConfigForge.Blazor.Components;
using ConfigForge.Blazor.Services;
using ConfigForge.Core;
using ConfigForge.Core.Documents;
using ConfigForge.Core.Schema;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ConfigForge.Blazor.Tests;

/// <summary>
/// An array-of-objects item can bundle its flattened nested-object properties into
/// labelled sections (<c>x-section</c>) so a reader can tell them apart, for example a
/// "Baseline" repository block next to a "Compare point" repository block within one row.
/// </summary>
public sealed class ArrayObjectSectionHeadingTests : BunitContext
{
    public ArrayObjectSectionHeadingTests()
    {
        Services.AddLogging();
        Services.AddConfigForgeCore();
        Services.AddConfigForgeBlazor();
    }

    private const string SectionedSchema = """
        {
          "schema": {
            "type": "object",
            "properties": {
              "Pairs": {
                "type": "array",
                "title": "Pairs",
                "items": {
                  "type": "object",
                  "properties": {
                    "Baseline": {
                      "type": "object",
                      "x-section": "Baseline",
                      "properties": {
                        "Url": { "type": "string", "title": "Baseline URL" }
                      }
                    },
                    "Source": {
                      "type": "object",
                      "x-section": "Compare point",
                      "properties": {
                        "Url": { "type": "string", "title": "Compare point URL" }
                      }
                    },
                    "Note": { "type": "string", "title": "Note" }
                  }
                }
              }
            }
          },
          "x-cf": { "id": "sectioned-ui", "name": "SectionedUi", "version": "1.0.0" }
        }
        """;

    private (IRenderedComponent<ConfigForgeShell> Cut, EditingSession Session) RenderShell()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        IConfigDocumentGenerator generator =
            Services.GetRequiredService<IConfigDocumentGenerator>();
        ConfigSchema schema = parser.Parse(SectionedSchema);

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters
                .Add(p => p.Schema, schema)
                .Add(p => p.Document, generator.GenerateEmpty(schema))
        );

        return (cut, Services.GetRequiredService<EditingSession>());
    }

    [Fact]
    public async Task ArrayObjectRow_RendersOneHeadingPerSection_InDeclarationOrder()
    {
        (IRenderedComponent<ConfigForgeShell> cut, _) = RenderShell();
        await cut.Find("button.cf-arrayobject-add").ClickAsync(new MouseEventArgs());

        var headings = cut.FindAll(".cf-section-heading");

        Assert.Equal(2, headings.Count);
        Assert.Equal("Baseline", headings[0].TextContent);
        Assert.Equal("Compare point", headings[1].TextContent);
    }

    [Fact]
    public async Task ArrayObjectRow_FieldWithoutSection_GetsNoHeading()
    {
        (IRenderedComponent<ConfigForgeShell> cut, _) = RenderShell();
        await cut.Find("button.cf-arrayobject-add").ClickAsync(new MouseEventArgs());

        var headings = cut.FindAll(".cf-section-heading");

        Assert.DoesNotContain(headings, heading => heading.TextContent == "Note");
    }

    [Fact]
    public async Task ArrayObjectRow_MultipleRows_EachGetsItsOwnHeadings()
    {
        (IRenderedComponent<ConfigForgeShell> cut, _) = RenderShell();
        await cut.Find("button.cf-arrayobject-add").ClickAsync(new MouseEventArgs());
        await cut.Find("button.cf-arrayobject-add").ClickAsync(new MouseEventArgs());

        var headings = cut.FindAll(".cf-section-heading");

        Assert.Equal(4, headings.Count);
    }
}
