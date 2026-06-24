using ConfigForge.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ConfigForge.Blazor.Services;

/// <summary>
/// Dependency-injection registration for the ConfigForge Blazor UI services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the ConfigForge Blazor services: the action dispatcher (singleton)
    /// the per-circuit editing session (scoped), and a fallback theme provider.
    /// Assumes <c>AddConfigForgeCore</c> has already been called for the core
    /// services. The theme provider is registered with <c>TryAddSingleton</c> so a
    /// host can override it by registering its own <see cref="IThemeProvider"/>.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddConfigForgeBlazor(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IActionDispatcher, ActionDispatcher>();
        services.TryAddScoped<EditingSession>();
        services.TryAddSingleton<IThemeProvider, DefaultThemeProvider>();

        return services;
    }
}
