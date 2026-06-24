using Bunit;
using ConfigForge.Abstractions;
using ConfigForge.Blazor.Components;
using ConfigForge.Blazor.Services;
using ConfigForge.Core;
using ConfigForge.Core.Documents;
using ConfigForge.Core.Plugins;
using ConfigForge.Core.Schema;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ConfigForge.Blazor.Tests;

/// <summary>
/// bUnit tests covering the four Blazor gap fixes: schema-driven action buttons,
/// per-field plugin-validator errors, and the malformed-JSON raw-editor fallback.
/// </summary>
public sealed class GapFixTests : BunitContext
{
    public GapFixTests()
    {
        Services.AddLogging();
        Services.AddConfigForgeCore();
        Services.AddConfigForgeBlazor();
    }

    [Fact]
    public void ActionButtonBar_SchemaAction_RendersDispatchingButton()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(ActionSchema);

        var registry = Services.GetRequiredService<PluginRegistry>();
        var invoked = false;
        registry.RegisterAction(
            "test.action",
            _ =>
            {
                invoked = true;
                return Task.CompletedTask;
            }
        );

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema)
        );

        var button = cut.Find(".cf-action-bar button");
        Assert.Contains("Run It", button.TextContent, StringComparison.Ordinal);
        Assert.Contains(
            "cf-button-primary",
            button.GetAttribute("class") ?? string.Empty,
            StringComparison.Ordinal
        );

        button.Click();

        Assert.True(invoked);
    }

    [Fact]
    public void FieldRenderer_PluginValidatorFails_ShowsInlineError()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(ValidatorSchema);

        var registry = Services.GetRequiredService<PluginRegistry>();
        registry.RegisterValidator("test.validator", _ => ValidationResult.Fail("Bad value!"));

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema)
        );

        Assert.DoesNotContain("cf-field-error", cut.Markup, StringComparison.Ordinal);

        cut.Find(".cf-field input").Change("anything");

        Assert.Contains("cf-field-error", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Bad value!", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigCanvas_MalformedJson_ShowsRawEditor()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(ValidatorSchema);

        IConfigDocumentEngine engine = Services.GetRequiredService<IConfigDocumentEngine>();
        const string Malformed = "{ not valid json";
        ConfigDocumentParseResult parseResult = engine.Parse(Malformed, schema);

        Assert.NotNull(parseResult.JsonError);

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters
                .Add(p => p.Schema, schema)
                .Add(p => p.Document, parseResult.Document)
                .Add(p => p.ParseResult, parseResult)
                .Add(p => p.RawDocumentJson, Malformed)
        );

        var editor = cut.Find("textarea.cf-raw-editor");
        Assert.Equal(Malformed, editor.GetAttribute("value"));
        Assert.Contains("cf-raw-error", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void FieldRenderer_PluginCustomControl_IsRendered()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(CustomControlSchema);

        var registry = Services.GetRequiredService<PluginRegistry>();
        registry.RegisterControl("custom.widget", typeof(MarkerControl));

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema)
        );

        Assert.Contains("plugin-marker", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("PLUGIN:name", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void FieldRenderer_PluginControl_ClickUpdatesValueAndReRenders()
    {
        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(ToggleSchema);

        var registry = Services.GetRequiredService<PluginRegistry>();
        registry.RegisterControl("test.toggle", typeof(ToggleControl));

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema)
        );

        Assert.Contains("state:off", cut.Markup, StringComparison.Ordinal);

        cut.Find("button.toggle-btn").Click();

        Assert.Contains("state:on", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>A plugin control with an interactive button that flips a field value.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1812",
        Justification = "Instantiated via DynamicComponent reflection, not directly."
    )]
    private sealed class ToggleControl : ComponentBase, IConfigControl
    {
        [Parameter]
        public ControlDescriptor Control { get; set; } = new();

        [Parameter]
        public ConfigDocument Document { get; set; } = new();

        [Parameter]
        public EventCallback<FieldChangedArgs> OnFieldChanged { get; set; }

        private string Current =>
            string.Equals(Document.GetString(Control.Key), "on", StringComparison.Ordinal)
                ? "on"
                : "off";

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "button");
            builder.AddAttribute(1, "class", "toggle-btn");
            builder.AddAttribute(2, "onclick", EventCallback.Factory.Create(this, Toggle));
            builder.AddContent(3, $"state:{Current}");
            builder.CloseElement();
        }

        private Task Toggle() =>
            OnFieldChanged.InvokeAsync(
                new FieldChangedArgs { Key = Control.Key, Value = Current == "on" ? "off" : "on" }
            );
    }

    private const string ToggleSchema = """
        {
          "schema": { "type": "object", "properties": { "flag": { "type": "string", "title": "Flag" } } },
          "uiSchema": {
            "type": "Categorization",
            "elements": [
              { "type": "Category", "label": "General", "elements": [ { "type": "Control", "scope": "#/properties/flag" } ] }
            ]
          },
          "x-cf": { "id": "tgl", "name": "Toggle", "controls": { "flag": { "type": "test.toggle" } } }
        }
        """;

    /// <summary>A minimal plugin control used to prove the RegisterControl path.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1812",
        Justification = "Instantiated via DynamicComponent reflection, not directly."
    )]
    private sealed class MarkerControl : ComponentBase, IConfigControl
    {
        [Parameter]
        public ControlDescriptor Control { get; set; } = new();

        [Parameter]
        public ConfigDocument Document { get; set; } = new();

        [Parameter]
        public EventCallback<FieldChangedArgs> OnFieldChanged { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "plugin-marker");
            builder.AddContent(2, $"PLUGIN:{Control.Key}");
            builder.CloseElement();
        }
    }

    private const string CustomControlSchema = """
        {
          "schema": { "type": "object", "properties": { "name": { "type": "string", "title": "Name" } } },
          "uiSchema": {
            "type": "Categorization",
            "elements": [
              { "type": "Category", "label": "General", "elements": [ { "type": "Control", "scope": "#/properties/name" } ] }
            ]
          },
          "x-cf": {
            "id": "ctl-schema",
            "name": "Custom Control Schema",
            "controls": { "name": { "type": "custom.widget" } }
          }
        }
        """;

    private const string ActionSchema = """
        {
          "schema": { "type": "object", "properties": { "name": { "type": "string", "title": "Name" } } },
          "uiSchema": {
            "type": "Categorization",
            "elements": [
              { "type": "Category", "label": "General", "elements": [ { "type": "Control", "scope": "#/properties/name" } ] }
            ]
          },
          "x-cf": {
            "id": "act-schema",
            "name": "Action Schema",
            "actions": [ { "actionId": "test.action", "label": "Run It", "variant": "primary" } ]
          }
        }
        """;

    private const string ValidatorSchema = """
        {
          "schema": { "type": "object", "properties": { "name": { "type": "string", "title": "Name" } } },
          "uiSchema": {
            "type": "Categorization",
            "elements": [
              { "type": "Category", "label": "General", "elements": [ { "type": "Control", "scope": "#/properties/name" } ] }
            ]
          },
          "x-cf": {
            "id": "val-schema",
            "name": "Validator Schema",
            "controls": { "name": { "validatorId": "test.validator" } }
          }
        }
        """;
}
