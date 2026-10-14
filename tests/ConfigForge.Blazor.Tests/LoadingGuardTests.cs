using Bunit;
using ConfigForge.Abstractions;
using ConfigForge.Blazor.Components;
using ConfigForge.Blazor.Services;
using ConfigForge.Core;
using ConfigForge.Core.Documents;
using ConfigForge.Core.Plugins;
using ConfigForge.Core.Schema;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ConfigForge.Blazor.Tests;

/// <summary>
/// While loader-driven data of the displayed area is still loading, editing and saving are held
/// back, and a loader that never answers must not keep the editor locked.
/// </summary>
public sealed class LoadingGuardTests : BunitContext
{
    private const string Schema = """
        {
          "schema": {
            "type": "object",
            "properties": {
              "Name": { "type": "string" },
              "Channel": { "type": "string", "x-control": "select", "x-loader": "slow" }
            }
          },
          "x-cf": { "id": "loading-guard" }
        }
        """;

    public LoadingGuardTests()
    {
        Services.AddLogging();
        Services.AddConfigForgeCore();
        Services.AddConfigForgeBlazor();
    }

    private IRenderedComponent<ConfigForgeShell> RenderShell(
        TaskCompletionSource<IReadOnlyList<SelectOption>> loader,
        TimeSpan? timeout = null
    )
    {
        Services.GetRequiredService<IPluginRegistry>().RegisterLoader("slow", _ => loader.Task);

        if (timeout is { } value)
        {
            Services.GetRequiredService<EditingSession>().OptionLoadTimeout = value;
        }

        IJsonFormsSchemaParser parser = Services.GetRequiredService<IJsonFormsSchemaParser>();
        ConfigSchema schema = parser.Parse(Schema);
        ConfigDocument document = Services
            .GetRequiredService<IConfigDocumentGenerator>()
            .GenerateEmpty(schema);

        return Render<ConfigForgeShell>(parameters =>
            parameters
                .Add(p => p.Schema, schema)
                .Add(p => p.Document, document)
                .Add(p => p.OnSave, EventCallback.Factory.Create<ConfigDocument>(this, _ => { }))
        );
    }

    [Fact]
    public void WhileLoading_ShowsBannerLocksEditingAndBlocksSave()
    {
        TaskCompletionSource<IReadOnlyList<SelectOption>> loader = new();
        IRenderedComponent<ConfigForgeShell> cut = RenderShell(loader);
        EditingSession session = Services.GetRequiredService<EditingSession>();

        session.SetFieldValue("Name", "changed");
        cut.Render();

        Assert.True(session.IsLoading);
        Assert.NotEmpty(cut.FindAll(".cf-loading-banner"));
        Assert.NotNull(cut.Find("fieldset.cf-canvas-lock").GetAttribute("disabled"));
        Assert.NotNull(cut.Find(".cf-save-actions .cf-button-primary").GetAttribute("disabled"));
    }

    [Fact]
    public void AfterLoading_UnlocksEditingAndSave()
    {
        TaskCompletionSource<IReadOnlyList<SelectOption>> loader = new();
        IRenderedComponent<ConfigForgeShell> cut = RenderShell(loader);
        EditingSession session = Services.GetRequiredService<EditingSession>();

        loader.SetResult([new SelectOption { Value = "a", Label = "A" }]);
        cut.WaitForAssertion(() => Assert.False(session.IsLoading));
        session.SetFieldValue("Name", "changed");
        cut.Render();

        Assert.Empty(cut.FindAll(".cf-loading-banner"));
        Assert.Null(cut.Find("fieldset.cf-canvas-lock").GetAttribute("disabled"));
        Assert.Null(cut.Find(".cf-save-actions .cf-button-primary").GetAttribute("disabled"));
    }

    [Fact]
    public void LoaderThatNeverAnswers_TimesOutUnlocksAndOffersRetry()
    {
        TaskCompletionSource<IReadOnlyList<SelectOption>> loader = new();
        IRenderedComponent<ConfigForgeShell> cut = RenderShell(
            loader,
            TimeSpan.FromMilliseconds(100)
        );
        EditingSession session = Services.GetRequiredService<EditingSession>();

        cut.WaitForAssertion(() => Assert.False(session.IsLoading), TimeSpan.FromSeconds(5));

        Assert.True(session.IsFieldLoadFailed("Channel"));
        cut.WaitForAssertion(() =>
            Assert.Contains(
                "could not be loaded",
                cut.Find(".cf-load-failed").TextContent,
                StringComparison.Ordinal
            )
        );
        Assert.Null(cut.Find("fieldset.cf-canvas-lock").GetAttribute("disabled"));
    }

    [Fact]
    public void Retry_AfterTimeout_LoadsOptionsAndClearsFailure()
    {
        TaskCompletionSource<IReadOnlyList<SelectOption>> loader = new();
        IRenderedComponent<ConfigForgeShell> cut = RenderShell(
            loader,
            TimeSpan.FromMilliseconds(100)
        );
        EditingSession session = Services.GetRequiredService<EditingSession>();
        cut.WaitForAssertion(() => Assert.True(session.IsFieldLoadFailed("Channel")));

        session.OptionLoadTimeout = TimeSpan.FromSeconds(30);
        cut.WaitForElement(".cf-load-failed button").Click();
        loader.SetResult([new SelectOption { Value = "a", Label = "A" }]);

        cut.WaitForAssertion(() =>
        {
            Assert.False(session.IsLoading);
            Assert.False(session.IsFieldLoadFailed("Channel"));
            Assert.Empty(cut.FindAll(".cf-load-failed"));
            Assert.NotEmpty(cut.FindAll("select option[value='a']"));
        });
    }
}
