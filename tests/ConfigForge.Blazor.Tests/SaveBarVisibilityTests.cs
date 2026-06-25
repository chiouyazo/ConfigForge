using Bunit;
using ConfigForge.Abstractions;
using ConfigForge.Blazor.Components;
using ConfigForge.Blazor.Services;
using ConfigForge.Core;
using ConfigForge.Core.Documents;
using ConfigForge.Core.Schema;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ConfigForge.Blazor.Tests;

/// <summary>
/// The save bar is pointless without somewhere to save to, so it only renders when
/// an <see cref="ConfigForgeShell.OnSave"/> handler is wired.
/// </summary>
public sealed class SaveBarVisibilityTests : BunitContext
{
    public SaveBarVisibilityTests()
    {
        Services.AddLogging();
        Services.AddConfigForgeCore();
        Services.AddConfigForgeBlazor();
    }

    private const string Schema = """
        {
          "schema": { "type": "object", "properties": { "Endpoint": { "type": "string" } } },
          "x-cf": { "id": "save-bar" }
        }
        """;

    private (ConfigSchema Schema, ConfigDocument Doc) Build()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        IConfigDocumentGenerator generator =
            Services.GetRequiredService<IConfigDocumentGenerator>();
        ConfigSchema schema = parser.Parse(Schema);
        return (schema, generator.GenerateEmpty(schema));
    }

    [Fact]
    public void NoSaveHandler_HidesSaveBarEvenWhenDirty()
    {
        (ConfigSchema schema, ConfigDocument doc) = Build();

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema).Add(p => p.Document, doc)
        );

        Services.GetRequiredService<EditingSession>().SetFieldValue("Endpoint", "x");
        cut.Render();

        Assert.Empty(cut.FindAll(".cf-save-bar"));
    }

    [Fact]
    public void WithSaveHandler_ShowsSaveBarWhenDirty()
    {
        (ConfigSchema schema, ConfigDocument doc) = Build();

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters
                .Add(p => p.Schema, schema)
                .Add(p => p.Document, doc)
                .Add(p => p.OnSave, EventCallback.Factory.Create<ConfigDocument>(this, _ => { }))
        );

        Services.GetRequiredService<EditingSession>().SetFieldValue("Endpoint", "x");
        cut.Render();

        Assert.NotEmpty(cut.FindAll(".cf-save-bar"));
    }
}
