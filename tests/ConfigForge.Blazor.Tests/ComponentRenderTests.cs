using Bunit;
using ConfigForge.Abstractions;
using ConfigForge.Blazor.Components.Fields;
using ConfigForge.Blazor.Services;
using ConfigForge.Core;
using ConfigForge.Core.Schema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ConfigForge.Blazor.Tests;

/// <summary>
/// bUnit render/behaviour tests for the ConfigForge Blazor components. The
/// container mirrors a real host: AddConfigForgeCore + AddConfigForgeBlazor.
/// </summary>
public sealed class ComponentRenderTests : BunitContext
{
    public ComponentRenderTests()
    {
        Services.AddLogging();
        Services.AddConfigForgeCore();
        Services.AddConfigForgeBlazor();
    }

    [Fact]
    public void UnknownFieldFallback_UnregisteredControlType_RendersWithoutThrowing()
    {
        var control = new ControlDescriptor { Key = "mystery", ControlType = "no-such-control" };
        var document = new ConfigDocument { ["mystery"] = "preserved-value" };

        IRenderedComponent<UnknownFieldFallback> cut = Render<UnknownFieldFallback>(parameters =>
            parameters.Add(p => p.Control, control).Add(p => p.Document, document)
        );

        Assert.Contains("no-such-control", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("preserved-value", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void TextField_RendersCurrentValue()
    {
        var control = new ControlDescriptor { Key = "name", ControlType = "text" };
        var document = new ConfigDocument { ["name"] = "hello" };

        IRenderedComponent<TextField> cut = Render<TextField>(parameters =>
            parameters.Add(p => p.Control, control).Add(p => p.Document, document)
        );

        var input = cut.Find("input");
        Assert.Equal("hello", input.GetAttribute("value"));
    }

    [Fact]
    public void TextField_OnInput_RaisesOnFieldChangedWithNewValue()
    {
        var control = new ControlDescriptor { Key = "name", ControlType = "text" };
        var document = new ConfigDocument { ["name"] = "hello" };
        FieldChangedArgs? captured = null;

        IRenderedComponent<TextField> cut = Render<TextField>(parameters =>
            parameters
                .Add(p => p.Control, control)
                .Add(p => p.Document, document)
                .Add(p => p.OnFieldChanged, args => captured = args)
        );

        cut.Find("input").Change("world");

        Assert.NotNull(captured);
        Assert.Equal("name", captured!.Key);
        Assert.Equal("world", captured.Value);
    }
}
