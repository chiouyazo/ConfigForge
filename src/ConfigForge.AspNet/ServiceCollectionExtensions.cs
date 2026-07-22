using ConfigForge.Abstractions;
using ConfigForge.Blazor.Services;
using ConfigForge.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace ConfigForge.AspNet;

/// <summary>
/// Dependency-injection registration for hosting ConfigForge inside ASP.NET Core.
/// </summary>
/// <remarks>
/// The spec's single-call integration is split into two coordinated calls because
/// services must be registered before the container is built, whereas middleware is
/// wired afterwards: call <see cref="AddConfigForge(IServiceCollection, System.Action{AspNetConfigForgeOptions})"/>
/// in <c>ConfigureServices</c> and
/// <see cref="ApplicationBuilderExtensions.UseConfigForge(Microsoft.AspNetCore.Builder.IApplicationBuilder)"/>
/// in <c>Configure</c>.
/// </remarks>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the ConfigForge core and Blazor services plus the ASP.NET hosting
    /// services: the resolved options, the singleton
    /// <see cref="IConfigForgeHostState"/>, the configured
    /// <see cref="IThemeProvider"/>, and the
    /// <see cref="ConfigForgeDirectoryWatcher"/> hosted service.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configure">A delegate that populates the options.</param>
    /// <returns>The same collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// <see cref="ConfigForgeMode.Locked"/> mode is selected without an
    /// <see cref="AspNetConfigForgeOptions.OnSave"/> handler.
    /// </exception>
    public static IServiceCollection AddConfigForge(
        this IServiceCollection services,
        Action<AspNetConfigForgeOptions> configure
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new AspNetConfigForgeOptions();
        configure(options);
        ValidateOptions(options);

        services.TryAddSingleton<IThemeProvider>(options.ThemeProvider);

        services.AddConfigForgeCore();
        services.AddConfigForgeBlazor();

        services.TryAddSingleton(options);
        services.TryAddSingleton<IConfigForgeHostState>(new ConfigForgeHostState(options));

        // Secret protection is opt-in: the gateway picks up an IConfigSecretProtector when the host
        // registered one, otherwise it is a transparent pass-through.
        services.TryAddSingleton(sp => new ConfigSecretGateway(
            sp.GetService<IConfigSecretProtector>()
        ));

        services.AddHostedService<ConfigForgeDirectoryWatcher>();

        if (!string.IsNullOrEmpty(options.SchemaUrl))
        {
            services.AddHttpClient();
            services.AddHostedService<RemoteSchemaPoller>();
        }

        services.AddRazorComponents().AddInteractiveServerComponents();

        return services;
    }

    /// <summary>
    /// Validates a populated options instance. <see cref="ConfigForgeMode.Locked"/>
    /// requires an <see cref="AspNetConfigForgeOptions.OnSave"/> handler.
    /// </summary>
    /// <param name="options">The options to validate.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The options are invalid.</exception>
    internal static void ValidateOptions(AspNetConfigForgeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Mode == ConfigForgeMode.Locked && options.OnSave is null)
        {
            throw new InvalidOperationException(
                "ConfigForge is configured in Locked mode, which requires an OnSave handler. "
                    + "Set AspNetConfigForgeOptions.OnSave or switch to ConfigForgeMode.Open."
            );
        }
    }
}
