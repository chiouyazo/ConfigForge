using Bunit;
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
/// The code panel's Config tab is an editable JSON view: pasting/editing JSON parses
/// straight back into the live document (the "paste an existing config" flow), and
/// invalid JSON surfaces an error without disturbing the form.
/// </summary>
public sealed class CodePanelEditTests : BunitContext
{
    public CodePanelEditTests()
    {
        Services.AddLogging();
        Services.AddConfigForgeCore();
        Services.AddConfigForgeBlazor();
    }

    private const string Schema = """
        {
          "schema": { "type": "object", "properties": { "Endpoint": { "type": "string" } } },
          "x-cf": { "id": "code-edit" }
        }
        """;

    private (IRenderedComponent<ConfigForgeShell> Cut, EditingSession Session) RenderWithPanelOpen()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        IConfigDocumentGenerator generator =
            Services.GetRequiredService<IConfigDocumentGenerator>();
        ConfigSchema schema = parser.Parse(Schema);

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters
                .Add(p => p.Schema, schema)
                .Add(p => p.Document, generator.GenerateEmpty(schema))
        );

        cut.FindAll("button")
            .First(b => b.TextContent.Contains("View code", StringComparison.Ordinal))
            .Click();

        return (cut, Services.GetRequiredService<EditingSession>());
    }

    [Fact]
    public void EditingConfigJson_AppliesToTheDocument()
    {
        (IRenderedComponent<ConfigForgeShell> cut, EditingSession session) = RenderWithPanelOpen();

        cut.Find("textarea.cf-codepanel-editor")
            .Change("{ \"Endpoint\": \"https://pasted.example.com\" }");

        Assert.Equal("https://pasted.example.com", session.Document["Endpoint"]);
    }

    [Fact]
    public void InvalidJson_ShowsErrorAndLeavesDocumentUntouched()
    {
        (IRenderedComponent<ConfigForgeShell> cut, EditingSession session) = RenderWithPanelOpen();

        cut.Find("textarea.cf-codepanel-editor").Change("{ \"Endpoint\": \"ok\" }");
        cut.Find("textarea.cf-codepanel-editor").Change("{ broken");

        Assert.NotEmpty(cut.FindAll(".cf-codepanel-error"));
        Assert.Equal("ok", session.Document["Endpoint"]);
    }
}
