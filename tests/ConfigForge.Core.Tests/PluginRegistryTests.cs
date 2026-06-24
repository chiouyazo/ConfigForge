using ConfigForge.Abstractions;
using ConfigForge.Core.Plugins;
using Xunit;

namespace ConfigForge.Core.Tests;

public sealed class PluginRegistryTests
{
    [Fact]
    public void RegisterAndResolveAction()
    {
        var registry = new PluginRegistry();
        Func<IActionContext, Task> handler = _ => Task.CompletedTask;

        registry.RegisterAction("act", handler);

        Assert.True(registry.TryGetAction("act", out Func<IActionContext, Task>? resolved));
        Assert.Same(handler, resolved);
    }

    [Fact]
    public void RegisterAndResolveLoader()
    {
        var registry = new PluginRegistry();
        Func<IActionContext, Task<IReadOnlyList<SelectOption>>> handler = _ =>
            Task.FromResult<IReadOnlyList<SelectOption>>([]);

        registry.RegisterLoader("load", handler);

        Assert.True(
            registry.TryGetLoader(
                "load",
                out Func<IActionContext, Task<IReadOnlyList<SelectOption>>>? resolved
            )
        );
        Assert.Same(handler, resolved);
    }

    [Fact]
    public void RegisterAndResolveValidator()
    {
        var registry = new PluginRegistry();
        Func<object?, ValidationResult> handler = _ => ValidationResult.Ok();

        registry.RegisterValidator("val", handler);

        Assert.True(registry.TryGetValidator("val", out Func<object?, ValidationResult>? resolved));
        Assert.Same(handler, resolved);
    }

    [Fact]
    public void TryGetAction_UnknownId_ReturnsFalse()
    {
        var registry = new PluginRegistry();
        Assert.False(registry.TryGetAction("missing", out Func<IActionContext, Task>? handler));
        Assert.Null(handler);
    }

    [Fact]
    public void TryGetLoader_UnknownId_ReturnsFalse()
    {
        var registry = new PluginRegistry();
        Assert.False(registry.TryGetLoader("missing", out _));
    }

    [Fact]
    public void TryGetValidator_UnknownId_ReturnsFalse()
    {
        var registry = new PluginRegistry();
        Assert.False(registry.TryGetValidator("missing", out _));
    }

    [Fact]
    public void LoadEmbedded_TracksPluginAndAppliesRegistrations()
    {
        var registry = new PluginRegistry();
        var loader = new PluginLoader(registry);
        var plugin = new FakePlugin();

        loader.LoadEmbedded(plugin);

        Assert.Contains("fake-plugin", registry.RegisteredPluginIds);
        Assert.True(registry.TryGetAction("fake.action", out _));
        Assert.True(registry.TryGetLoader("fake.loader", out _));
        Assert.True(registry.TryGetValidator("fake.validator", out _));
    }

    private sealed class FakePlugin : IPlugin
    {
        public string Id => "fake-plugin";

        public string DisplayName => "Fake Plugin";

        public void Register(IPluginRegistry registry)
        {
            registry.RegisterAction("fake.action", _ => Task.CompletedTask);
            registry.RegisterLoader(
                "fake.loader",
                _ => Task.FromResult<IReadOnlyList<SelectOption>>([])
            );
            registry.RegisterValidator("fake.validator", _ => ValidationResult.Ok());
        }
    }
}
