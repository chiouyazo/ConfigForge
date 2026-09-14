using AngleSharp.Dom;
using Bunit;
using ConfigForge.Abstractions;
using ConfigForge.Blazor.Components;
using ConfigForge.Blazor.Services;
using ConfigForge.Core;
using ConfigForge.Core.Schema;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ConfigForge.Blazor.Tests;

public sealed class SidebarGroupHeadingRenderTests : BunitContext
{
    public SidebarGroupHeadingRenderTests()
    {
        Services.AddLogging();
        Services.AddConfigForgeCore();
        Services.AddConfigForgeBlazor();
    }

    [Fact]
    public void TwoGroupedCategories_RenderOneHeadingPerGroup_WithTheGroupHeadingClass()
    {
        var schema = new ConfigSchema
        {
            Id = "grouped",
            Categories =
            [
                new CategoryElement { Label = "Users", GroupLabel = "Instance One" },
                new CategoryElement { Label = "Orders", GroupLabel = "Instance One" },
                new CategoryElement { Label = "Users", GroupLabel = "Instance Two" },
            ],
        };

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema).Add(p => p.Document, new ConfigDocument())
        );

        IReadOnlyList<IElement> headings = cut.FindAll(".cf-category-group-heading");
        Assert.Equal(2, headings.Count);
        Assert.Equal("Instance One", headings[0].TextContent.Trim());
        Assert.Equal("Instance Two", headings[1].TextContent.Trim());
    }
}
