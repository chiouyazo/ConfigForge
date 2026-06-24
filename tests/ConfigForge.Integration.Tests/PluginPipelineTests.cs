using System.Reflection;
using ConfigForge.Core.Plugins;
using ConfigForge.Plugin.Template;
using Xunit;

namespace ConfigForge.Integration.Tests;

/// <summary>
/// End-to-end plugin discovery and registration through the loader and registry.
/// </summary>
public sealed class PluginPipelineTests
{
    private const string PluginId = "ConfigForge.Plugin.Example";

    [Fact]
    public void LoadEmbedded_RegistersExamplePluginAndItsExtensionPoints()
    {
        var registry = new PluginRegistry();
        var loader = new PluginLoader(registry);

        loader.LoadEmbedded(new ExamplePlugin());

        Assert.Contains(PluginId, registry.RegisteredPluginIds);
        Assert.True(registry.TryGetAction("example.testConnection", out _));
        Assert.True(registry.TryGetLoader("example.loadChannels", out _));
        Assert.True(registry.TryGetValidator("example.validateChannel", out _));
    }

    [Fact]
    public async Task LoadAssemblyAsync_DiscoversExamplePluginViaReflection()
    {
        string assemblyPath = typeof(ExamplePlugin).Assembly.Location;
        Assert.True(File.Exists(assemblyPath), $"Expected plugin assembly at '{assemblyPath}'.");

        var registry = new PluginRegistry();
        var loader = new PluginLoader(registry);

        await loader.LoadAssemblyAsync(assemblyPath);

        Assert.Contains(PluginId, registry.RegisteredPluginIds);
        Assert.True(registry.TryGetAction("example.testConnection", out _));
        Assert.True(registry.TryGetLoader("example.loadChannels", out _));
        Assert.True(registry.TryGetValidator("example.validateChannel", out _));
    }

    [Fact]
    public void ExamplePlugin_ExposesExpectedIdentity()
    {
        var plugin = new ExamplePlugin();

        Assert.Equal(PluginId, plugin.Id);
        Assert.False(string.IsNullOrWhiteSpace(plugin.DisplayName));
        Assert.True(typeof(Abstractions.IPlugin).IsAssignableFrom(typeof(ExamplePlugin)));
        Assert.False(typeof(ExamplePlugin).GetTypeInfo().IsAbstract);
    }
}
