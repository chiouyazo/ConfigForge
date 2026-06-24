using ConfigForge.Abstractions;
using ConfigForge.Core;
using ConfigForge.Core.Documents;
using ConfigForge.Core.Plugins;
using ConfigForge.Core.Schema;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ConfigForge.Integration.Tests;

/// <summary>
/// DI smoke test for the core service registrations.
/// </summary>
public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddConfigForgeCore_ResolvesAllCoreServices()
    {
        var services = new ServiceCollection();
        services.AddConfigForgeCore();

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IJsonFormsSchemaParser>());
        Assert.NotNull(provider.GetRequiredService<IConfigDocumentEngine>());
        Assert.NotNull(provider.GetRequiredService<IConfigDocumentGenerator>());
        Assert.NotNull(provider.GetRequiredService<IPluginRegistry>());
        Assert.NotNull(provider.GetRequiredService<IPluginCatalog>());
        Assert.NotNull(provider.GetRequiredService<IPluginLoader>());
    }

    [Fact]
    public void AddConfigForgeCore_RegistryAndCatalog_ResolveToSamePluginRegistryInstance()
    {
        var services = new ServiceCollection();
        services.AddConfigForgeCore();

        using ServiceProvider provider = services.BuildServiceProvider();

        var registry = provider.GetRequiredService<IPluginRegistry>();
        var catalog = provider.GetRequiredService<IPluginCatalog>();
        var concrete = provider.GetRequiredService<PluginRegistry>();

        Assert.Same(concrete, registry);
        Assert.Same(concrete, catalog);
        Assert.Same(registry, catalog);
    }
}
