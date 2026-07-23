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
/// The write-only secret list: stored entries show as masked removable rows, adding appends a new
/// plaintext element, and removing drops the element, all without exposing stored values.
/// </summary>
public sealed class SecretListFieldTests : BunitContext
{
    public SecretListFieldTests()
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
              "ApiKeys": {
                "type": "array",
                "items": { "type": "string", "x-control": "secret" },
                "x-control": "secretlist",
                "title": "API keys"
              }
            }
          },
          "x-cf": { "id": "sec" }
        }
        """;

    private (IRenderedComponent<ConfigForgeShell> Cut, EditingSession Session) RenderWith(
        object? apiKeys
    )
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(SchemaJson);
        ConfigDocument doc = new();
        if (apiKeys is not null)
        {
            doc["ApiKeys"] = apiKeys;
        }

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema).Add(p => p.Document, doc)
        );
        return (cut, Services.GetRequiredService<EditingSession>());
    }

    [Fact]
    public void ParsesAsSecretListWithSecretItemTemplate()
    {
        ConfigSchema schema = Services
            .GetRequiredService<IJsonFormsSchemaParser>()
            .Parse(SchemaJson);
        Assert.Equal("secretlist", schema.Fields["ApiKeys"].ControlType);
        Assert.Equal("secret", schema.Fields["ApiKeys"].ValueField?.ControlType);
    }

    [Fact]
    public void Empty_ShowsAddButton_AndNoStoredRows()
    {
        (IRenderedComponent<ConfigForgeShell> cut, _) = RenderWith(null);

        Assert.Contains(cut.FindAll(".cf-main button"), b => b.TextContent.Trim() == "Add");
        Assert.Empty(cut.FindAll(".cf-secret-list-item"));
    }

    [Fact]
    public void StoredEntries_RenderAsMaskedRemovableRows()
    {
        (IRenderedComponent<ConfigForgeShell> cut, _) = RenderWith(
            new[] { ConfigForgeSecret.IndexedMarker(0), ConfigForgeSecret.IndexedMarker(1) }
        );

        Assert.Equal(2, cut.FindAll(".cf-secret-list-item").Count);
        Assert.Equal(
            2,
            cut.FindAll(".cf-main button").Count(b => b.TextContent.Trim() == "Remove")
        );
        // The stored markers are shown as masked rows, never as the value of the add input.
        Assert.DoesNotContain(
            cut.FindAll(".cf-main input"),
            i =>
                i.GetAttribute("value")?.Contains("configforge-secret", StringComparison.Ordinal)
                == true
        );
    }

    [Fact]
    public void Add_AppendsPlaintextEntry()
    {
        (IRenderedComponent<ConfigForgeShell> cut, EditingSession session) = RenderWith(
            new[] { ConfigForgeSecret.IndexedMarker(0) }
        );

        cut.Find(".cf-main input").Input("new-key");
        cut.FindAll(".cf-main button").First(b => b.TextContent.Trim() == "Add").Click();

        object?[] keys = ((IEnumerable<object?>)session.Document["ApiKeys"]!).ToArray();
        Assert.Equal(2, keys.Length);
        Assert.Equal(ConfigForgeSecret.IndexedMarker(0), keys[0]);
        Assert.Equal("new-key", keys[1]);
        Assert.True(session.IsDirty);
    }

    [Fact]
    public void Remove_DropsTheEntry()
    {
        (IRenderedComponent<ConfigForgeShell> cut, EditingSession session) = RenderWith(
            new[] { ConfigForgeSecret.IndexedMarker(0), ConfigForgeSecret.IndexedMarker(1) }
        );

        cut.FindAll(".cf-main button").First(b => b.TextContent.Trim() == "Remove").Click();

        object?[] keys = ((IEnumerable<object?>)session.Document["ApiKeys"]!).ToArray();
        Assert.Single(keys);
        Assert.Equal(ConfigForgeSecret.IndexedMarker(1), keys[0]);
        Assert.True(session.IsDirty);
    }
}
