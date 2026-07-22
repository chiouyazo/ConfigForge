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
/// Reproduces the reported "SMTP value reverts" scenario: a field disabled by a rule
/// (here SmtpHost, enabled only while Alerts.Enabled is true) is edited, then the
/// rule is toggled off and back on. The edit must survive the disable/enable
/// re-render and not fall back to the loaded value or the schema default.
/// </summary>
public sealed class DisabledFieldPersistenceTests : BunitContext
{
    public DisabledFieldPersistenceTests()
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
              "Alerts": {
                "type": "object",
                "properties": {
                  "Enabled": { "type": "boolean", "title": "Enabled" },
                  "SmtpHost": { "type": "string", "title": "SMTP host", "default": "localhost" }
                }
              }
            }
          },
          "uiSchema": {
            "type": "Categorization",
            "elements": [
              {
                "type": "Category",
                "label": "Alerts",
                "elements": [
                  { "type": "Control", "scope": "#/properties/Alerts/properties/Enabled" },
                  {
                    "type": "Control",
                    "scope": "#/properties/Alerts/properties/SmtpHost",
                    "rule": {
                      "effect": "ENABLE",
                      "condition": {
                        "scope": "#/properties/Alerts/properties/Enabled",
                        "schema": { "const": true }
                      }
                    }
                  }
                ]
              }
            ]
          },
          "x-cf": { "id": "alerts" }
        }
        """;

    [Fact]
    public void EditedThenRuleToggled_KeepsTheEditedValue()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        IConfigDocumentEngine engine = Services.GetRequiredService<IConfigDocumentEngine>();
        ConfigSchema schema = parser.Parse(SchemaJson);

        // The "loaded" document: alerts on, SMTP host at the previously-saved value.
        ConfigDocument loaded = engine
            .Parse(
                "{ \"Alerts\": { \"Enabled\": true, \"SmtpHost\": \"smtp.example.com\" } }",
                schema
            )
            .Document;

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema).Add(p => p.Document, loaded)
        );
        EditingSession session = Services.GetRequiredService<EditingSession>();

        // The user types a real SMTP host into the (enabled) field.
        cut.Find(".cf-main input.cf-input").Change("mail-io.maxcluster.net");
        Assert.Equal("mail-io.maxcluster.net", session.Document["Alerts/SmtpHost"]);

        // Toggle the rule off (disables SmtpHost) and back on (re-enables it).
        session.SetFieldValue("Alerts/Enabled", false);
        cut.Render();
        session.SetFieldValue("Alerts/Enabled", true);
        cut.Render();

        // The edit must survive: not reverted to the loaded value or the schema default.
        Assert.Equal("mail-io.maxcluster.net", session.Document["Alerts/SmtpHost"]);
        Assert.Equal(
            "mail-io.maxcluster.net",
            cut.Find(".cf-main input.cf-input").GetAttribute("value")
        );

        // And it is what would be persisted on save.
        Assert.Contains(
            "mail-io.maxcluster.net",
            engine.Serialize(session.Document),
            StringComparison.Ordinal
        );
    }
}
