using ConfigForge.Abstractions;
using ConfigForge.Core.Documents;
using ConfigForge.Core.Plugins;
using ConfigForge.Core.Schema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ConfigForge.Core;

/// <summary>
/// Dependency-injection registration for the ConfigForge core services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the ConfigForge core services: schema parser, rule evaluator,
    /// document engine, document generator, plugin registry/catalog, plugin
    /// loader, and the dirty-state tracker.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddConfigForgeCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IJsonFormsSchemaParser, JsonFormsSchemaParser>();
        services.TryAddSingleton<IJsonFormsRuleEvaluator, JsonFormsRuleEvaluator>();
        services.TryAddSingleton<IConfigDocumentEngine, ConfigDocumentEngine>();
        services.TryAddSingleton<IConfigDocumentGenerator, ConfigDocumentGenerator>();

        services.TryAddSingleton<PluginRegistry>();
        services.TryAddSingleton<IPluginRegistry>(sp => sp.GetRequiredService<PluginRegistry>());
        services.TryAddSingleton<IPluginCatalog>(sp => sp.GetRequiredService<PluginRegistry>());
        services.TryAddSingleton<IPluginLoader, PluginLoader>();

        services.TryAddTransient<IDirtyStateTracker, DirtyStateTracker>();

        return services;
    }
}
