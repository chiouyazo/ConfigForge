using System;
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

/// <summary>
/// The oneof/discriminator control: a select picks the variant, the active
/// variant's fields render, switching the discriminator resets the sub-object
/// to just the chosen type, and editing a child field writes under the field key.
/// </summary>
public sealed class OneOfFieldTests : BunitContext
{
    public OneOfFieldTests()
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
              "Provider": {
                "oneOf": [
                  {
                    "type": "object",
                    "properties": {
                      "Type": { "const": "Alpha" },
                      "AccessToken": { "type": "string", "title": "AccessToken" }
                    }
                  },
                  {
                    "type": "object",
                    "properties": {
                      "Type": { "const": "Beta" },
                      "ClientId": { "type": "string", "title": "ClientId" }
                    }
                  }
                ]
              }
            }
          },
          "x-cf": { "id": "oneof" }
        }
        """;

    private (IRenderedComponent<ConfigForgeShell> Cut, EditingSession Session) RenderWith(
        object? shopValue
    )
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(SchemaJson);
        ConfigDocument doc = new();
        if (shopValue is not null)
        {
            doc["Provider"] = shopValue;
        }

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema).Add(p => p.Document, doc)
        );
        return (cut, Services.GetRequiredService<EditingSession>());
    }

    [Fact]
    public void DefaultsToFirstVariant_AndRendersItsChildren()
    {
        (IRenderedComponent<ConfigForgeShell> cut, _) = RenderWith(null);

        // The select carries both variants.
        System.Collections.Generic.IReadOnlyList<string> options =
        [
            .. cut.FindAll(".cf-oneof-select option").Select(o => o.TextContent.Trim()),
        ];
        Assert.Equal(["Alpha", "Beta"], options);

        // First variant's field renders; the other variant's does not.
        Assert.Contains(
            cut.FindAll("label"),
            l => l.TextContent.Contains("AccessToken", StringComparison.Ordinal)
        );
        Assert.DoesNotContain(
            cut.FindAll("label"),
            l => l.TextContent.Contains("ClientId", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void SwitchingVariant_ResetsToChosenType_AndRendersNewChildren()
    {
        (IRenderedComponent<ConfigForgeShell> cut, EditingSession session) = RenderWith(null);

        cut.Find(".cf-oneof-select").Change("Beta");

        Assert.Equal("Beta", session.Document["Provider/Type"]);
        Assert.Contains(
            cut.FindAll("label"),
            l => l.TextContent.Contains("ClientId", StringComparison.Ordinal)
        );
        Assert.DoesNotContain(
            cut.FindAll("label"),
            l => l.TextContent.Contains("AccessToken", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void EditingChildField_WritesUnderFieldKey()
    {
        (IRenderedComponent<ConfigForgeShell> cut, EditingSession session) = RenderWith(null);

        cut.Find(".cf-oneof-body input").Change("shpat_token");

        Assert.Equal("shpat_token", session.Document["Provider/AccessToken"]);
    }
}
