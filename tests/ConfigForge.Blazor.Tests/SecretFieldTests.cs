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
/// The write-only secret control: a stored secret shows Change/Clear without
/// exposing its value, changing it writes the new plaintext, clearing nulls it,
/// and leaving a stored secret untouched keeps the document clean.
/// </summary>
public sealed class SecretFieldTests : BunitContext
{
    public SecretFieldTests()
    {
        Services.AddLogging();
        Services.AddConfigForgeCore();
        Services.AddConfigForgeBlazor();
    }

    private const string SchemaJson = """
        {
          "schema": { "type": "object", "properties": { "ApiKey": { "type": "string", "title": "API key" } } },
          "x-cf": { "id": "sec", "controls": { "ApiKey": { "type": "secret" } } }
        }
        """;

    private (IRenderedComponent<ConfigForgeShell> Cut, EditingSession Session) RenderWith(
        string apiKeyValue
    )
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(SchemaJson);
        ConfigDocument doc = new();
        if (apiKeyValue is not null)
        {
            doc["ApiKey"] = apiKeyValue;
        }

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema).Add(p => p.Document, doc)
        );
        return (cut, Services.GetRequiredService<EditingSession>());
    }

    [Fact]
    public void Parser_MapsSecretControlType()
    {
        ConfigSchema schema = Services
            .GetRequiredService<IJsonFormsSchemaParser>()
            .Parse(SchemaJson);
        Assert.Equal("secret", schema.Fields["ApiKey"].ControlType);
    }

    [Fact]
    public void StoredSecret_ShowsChangeAndClear_HidesValue_AndStaysClean()
    {
        (IRenderedComponent<ConfigForgeShell> cut, EditingSession session) = RenderWith(
            ConfigForgeSecret.StoredMarker
        );

        Assert.Contains(cut.FindAll(".cf-main button"), b => b.TextContent.Trim() == "Change");
        Assert.Contains(cut.FindAll(".cf-main button"), b => b.TextContent.Trim() == "Clear");
        // The marker/value is never rendered into an input.
        Assert.Empty(cut.FindAll(".cf-main input"));
        Assert.False(session.IsDirty);
    }

    [Fact]
    public void Change_TypingNewValue_WritesPlaintext()
    {
        (IRenderedComponent<ConfigForgeShell> cut, EditingSession session) = RenderWith(
            ConfigForgeSecret.StoredMarker
        );

        cut.FindAll(".cf-main button").First(b => b.TextContent.Trim() == "Change").Click();
        cut.Find(".cf-main input").Change("new-plaintext-secret");

        Assert.Equal("new-plaintext-secret", session.Document["ApiKey"]);
        Assert.True(session.IsDirty);
    }

    [Fact]
    public void Clear_NullsTheSecret()
    {
        (IRenderedComponent<ConfigForgeShell> cut, EditingSession session) = RenderWith(
            ConfigForgeSecret.StoredMarker
        );

        cut.FindAll(".cf-main button").First(b => b.TextContent.Trim() == "Clear").Click();

        Assert.Null(session.Document["ApiKey"]);
        Assert.True(session.IsDirty);
    }

    [Fact]
    public void EmptySecret_ShowsSetButton()
    {
        (IRenderedComponent<ConfigForgeShell> cut, _) = RenderWith(null!);
        Assert.Contains(cut.FindAll(".cf-main button"), b => b.TextContent.Trim() == "Set secret");
    }
}
