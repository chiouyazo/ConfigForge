using Bunit;
using ConfigForge.Blazor.Components;
using ConfigForge.Blazor.Services;
using ConfigForge.Core;
using ConfigForge.Core.Documents;
using ConfigForge.Core.Schema;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ConfigForge.Blazor.Tests;

/// <summary>
/// Verifies that a field marked <c>tracked: false</c> is wired through the editing
/// session so its edits never mark the document dirty, while normal fields do.
/// </summary>
public sealed class UntrackedFieldRenderTests : BunitContext
{
    public UntrackedFieldRenderTests()
    {
        Services.AddLogging();
        Services.AddConfigForgeCore();
        Services.AddConfigForgeBlazor();
    }

    private const string Schema = """
        {
          "schema": {
            "type": "object",
            "properties": {
              "Endpoint": { "type": "string", "title": "Endpoint" },
              "TestRecipient": { "type": "string", "title": "Test recipient" }
            }
          },
          "x-cf": {
            "id": "untracked-ui",
            "controls": { "TestRecipient": { "tracked": false } }
          }
        }
        """;

    [Fact]
    public void UntrackedField_EditDoesNotMarkDirty_TrackedFieldDoes()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        IConfigDocumentGenerator generator =
            Services.GetRequiredService<IConfigDocumentGenerator>();
        ConfigSchema schema = parser.Parse(Schema);

        Render<ConfigForgeShell>(parameters =>
            parameters
                .Add(p => p.Schema, schema)
                .Add(p => p.Document, generator.GenerateEmpty(schema))
        );

        EditingSession session = Services.GetRequiredService<EditingSession>();
        Assert.False(session.IsDirty);

        session.SetFieldValue("TestRecipient", "tester@example.com");
        Assert.False(session.IsDirty);

        session.SetFieldValue("Endpoint", "https://api.example.com");
        Assert.True(session.IsDirty);
    }
}
