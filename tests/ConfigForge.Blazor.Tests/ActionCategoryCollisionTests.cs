using AngleSharp.Dom;
using Bunit;
using ConfigForge.Abstractions;
using ConfigForge.Blazor.Components;
using ConfigForge.Blazor.Services;
using ConfigForge.Core;
using ConfigForge.Core.Documents;
using ConfigForge.Core.Plugins;
using ConfigForge.Core.Schema;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ConfigForge.Blazor.Tests;

/// <summary>
/// Two different sources (e.g. two remote instances merged into one host schema) can legitimately
/// expose identically-labelled categories. Action-button resolution must key off
/// <see cref="CategoryElement.CategoryKey"/>/<see cref="ActionDefinition.CategoryKey"/>, not
/// <see cref="CategoryElement.Label"/> alone, so each tab shows and dispatches only its own
/// actions despite the label collision.
/// </summary>
public sealed class ActionCategoryCollisionTests : BunitContext
{
    public ActionCategoryCollisionTests()
    {
        Services.AddLogging();
        Services.AddConfigForgeCore();
        Services.AddConfigForgeBlazor();
    }

    private static ConfigSchema BuildCollidingSchema() =>
        new()
        {
            Id = "collision",
            Categories =
            [
                new CategoryElement { Label = "Users", CategoryKey = "instanceA::Users" },
                new CategoryElement { Label = "Users", CategoryKey = "instanceB::Users" },
            ],
            Actions =
            [
                new ActionDefinition
                {
                    ActionId = "instanceA::sync",
                    Label = "Sync A",
                    Category = "Users",
                    CategoryKey = "instanceA::Users",
                    Position = "top",
                },
                new ActionDefinition
                {
                    ActionId = "instanceB::sync",
                    Label = "Sync B",
                    Category = "Users",
                    CategoryKey = "instanceB::Users",
                    Position = "top",
                },
            ],
        };

    [Fact]
    public async Task EachCollidingCategory_ShowsOnlyItsOwnAction()
    {
        ConfigSchema schema = BuildCollidingSchema();
        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema).Add(p => p.Document, new ConfigDocument())
        );

        IReadOnlyList<IElement> tabs = cut.FindAll(".cf-category-item");
        Assert.Equal(2, tabs.Count);

        await tabs[0].ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.Render();
        Assert.Single(cut.FindAll(".cf-action-bar button"));
        Assert.Contains(
            "Sync A",
            cut.Find(".cf-action-bar button").TextContent,
            StringComparison.Ordinal
        );

        tabs = cut.FindAll(".cf-category-item");
        await tabs[1].ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.Render();
        Assert.Single(cut.FindAll(".cf-action-bar button"));
        Assert.Contains(
            "Sync B",
            cut.Find(".cf-action-bar button").TextContent,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task EachCollidingCategory_DispatchesOnlyItsOwnHandler()
    {
        ConfigSchema schema = BuildCollidingSchema();
        IPluginRegistry registry = Services.GetRequiredService<IPluginRegistry>();
        var invokedA = false;
        var invokedB = false;
        registry.RegisterAction(
            "instanceA::sync",
            _ =>
            {
                invokedA = true;
                return Task.CompletedTask;
            }
        );
        registry.RegisterAction(
            "instanceB::sync",
            _ =>
            {
                invokedB = true;
                return Task.CompletedTask;
            }
        );

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema).Add(p => p.Document, new ConfigDocument())
        );

        IReadOnlyList<IElement> tabs = cut.FindAll(".cf-category-item");
        await tabs[1].ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.Render();
        await cut.Find(".cf-action-bar button")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.True(invokedB);
        Assert.False(invokedA);
    }
}
