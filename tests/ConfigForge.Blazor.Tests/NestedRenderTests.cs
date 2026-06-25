using Bunit;
using ConfigForge.Blazor.Components;
using ConfigForge.Blazor.Services;
using ConfigForge.Core;
using ConfigForge.Core.Documents;
using ConfigForge.Core.Schema;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ConfigForge.Blazor.Tests;

/// <summary>
/// Renders the shell against a nested schema (nested object, array of objects, and
/// an additionalProperties map) and exercises the built-in composite controls,
/// asserting that edits land on the correct nested document paths.
/// </summary>
public sealed class NestedRenderTests : BunitContext
{
    public NestedRenderTests()
    {
        Services.AddLogging();
        Services.AddConfigForgeCore();
        Services.AddConfigForgeBlazor();
    }

    private const string NestedSchema = """
        {
          "schema": {
            "type": "object",
            "properties": {
              "ExchangeLock": {
                "type": "object",
                "properties": {
                  "ConnectionString": { "type": "string", "title": "Connection string" }
                }
              },
              "Targets": {
                "type": "array",
                "title": "Targets",
                "items": {
                  "type": "object",
                  "properties": {
                    "Name": { "type": "string", "title": "Name" },
                    "Headers": {
                      "type": "object",
                      "additionalProperties": { "type": "string" }
                    }
                  }
                }
              }
            }
          },
          "x-cf": { "id": "nested-ui", "name": "NestedUi", "version": "1.0.0" }
        }
        """;

    private (IRenderedComponent<ConfigForgeShell> Cut, EditingSession Session) RenderShell()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        IConfigDocumentGenerator generator =
            Services.GetRequiredService<IConfigDocumentGenerator>();
        ConfigSchema schema = parser.Parse(NestedSchema);

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters
                .Add(p => p.Schema, schema)
                .Add(p => p.Document, generator.GenerateEmpty(schema))
        );

        return (cut, Services.GetRequiredService<EditingSession>());
    }

    [Fact]
    public void NestedObjectLeaf_RendersControl()
    {
        (IRenderedComponent<ConfigForgeShell> cut, _) = RenderShell();

        Assert.Contains(
            cut.FindAll("label.cf-label"),
            label => label.TextContent.Contains("Connection string", StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task ArrayObject_AddRow_CreatesRowAndDocumentEntry()
    {
        (IRenderedComponent<ConfigForgeShell> cut, EditingSession session) = RenderShell();

        Assert.Empty(cut.FindAll(".cf-arrayobject-row"));

        await cut.Find("button.cf-arrayobject-add").ClickAsync(new MouseEventArgs());

        Assert.Single(cut.FindAll(".cf-arrayobject-row"));
        Assert.True(session.Document["Targets"] is IList<object?> list && list.Count == 1);
    }

    [Fact]
    public async Task ArrayObject_EditChildField_PersistsToNestedPath()
    {
        (IRenderedComponent<ConfigForgeShell> cut, EditingSession session) = RenderShell();
        await cut.Find("button.cf-arrayobject-add").ClickAsync(new MouseEventArgs());

        var nameInput = cut.Find(".cf-arrayobject-row .cf-field input.cf-input");
        await nameInput.ChangeAsync(new ChangeEventArgs { Value = "otlp-primary" });

        Assert.Equal("otlp-primary", session.Document["Targets/0/Name"]);
    }

    [Fact]
    public async Task Map_AddEntry_CreatesEntryUnderNestedPath()
    {
        (IRenderedComponent<ConfigForgeShell> cut, EditingSession session) = RenderShell();
        await cut.Find("button.cf-arrayobject-add").ClickAsync(new MouseEventArgs());

        await cut.Find("button.cf-map-add").ClickAsync(new MouseEventArgs());

        Assert.Single(cut.FindAll(".cf-map-row"));
        Assert.True(
            session.Document["Targets/0/Headers"] is IDictionary<string, object?> headers
                && headers.Count == 1
        );
    }

    [Fact]
    public async Task Map_EditValue_PersistsToNestedPath()
    {
        (IRenderedComponent<ConfigForgeShell> cut, EditingSession session) = RenderShell();
        await cut.Find("button.cf-arrayobject-add").ClickAsync(new MouseEventArgs());
        await cut.Find("button.cf-map-add").ClickAsync(new MouseEventArgs());

        var keyInput = cut.Find(".cf-map-row input.cf-map-key");
        await keyInput.ChangeAsync(new ChangeEventArgs { Value = "Authorization" });

        var valueInput = cut.Find(".cf-map-row .cf-map-value input.cf-input");
        await valueInput.ChangeAsync(new ChangeEventArgs { Value = "Bearer token" });

        Assert.Equal("Bearer token", session.Document["Targets/0/Headers/Authorization"]);
    }
}
