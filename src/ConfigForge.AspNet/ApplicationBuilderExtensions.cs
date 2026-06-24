using ConfigForge.Abstractions;
using ConfigForge.AspNet.Components;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ConfigForge.AspNet;

/// <summary>
/// Middleware registration that mounts the interactive ConfigForge Blazor UI under
/// the configured path prefix.
/// </summary>
/// <remarks>
/// <para>
/// Hosting Blazor Server from a reusable middleware package is unusual: the Blazor
/// circuit (SignalR hub) and the static-asset endpoints are registered at the app
/// level rather than isolated behind <c>app.Map(prefix, …)</c>, because branching the
/// pipeline on the prefix breaks the hub negotiation and <c>_framework</c> asset
/// resolution. The configured <see cref="AspNetConfigForgeOptions.PathPrefix"/> is
/// instead honoured by the host document's <c>&lt;base href&gt;</c> and the page
/// route, so the rendered UI lives under the prefix while the framework plumbing
/// stays at the root.
/// </para>
/// </remarks>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Mounts the interactive ConfigForge UI using the options registered by
    /// <see cref="ServiceCollectionExtensions.AddConfigForge(IServiceCollection, System.Action{AspNetConfigForgeOptions})"/>.
    /// </summary>
    /// <param name="app">The application builder. Must be an endpoint route builder
    /// (a <see cref="WebApplication"/>) so the Blazor component endpoints can be mapped.</param>
    /// <returns>The same application builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// <see cref="ServiceCollectionExtensions.AddConfigForge(IServiceCollection, System.Action{AspNetConfigForgeOptions})"/>
    /// was not called during service registration, or <paramref name="app"/> is not an
    /// <see cref="IEndpointRouteBuilder"/>.
    /// </exception>
    public static IApplicationBuilder UseConfigForge(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        _ =
            app.ApplicationServices.GetService<IConfigForgeHostState>()
            ?? throw new InvalidOperationException(
                "UseConfigForge requires AddConfigForge to have been called in ConfigureServices."
            );

        return MapUi(app);
    }

    /// <summary>
    /// Mounts the interactive ConfigForge UI, building the options from the supplied
    /// delegate.
    /// </summary>
    /// <param name="app">The application builder. Must be an endpoint route builder
    /// (a <see cref="WebApplication"/>).</param>
    /// <param name="configure">A delegate that populates the options.</param>
    /// <returns>The same application builder, for chaining.</returns>
    /// <remarks>
    /// This overload supports the spec's single-call style. The supplied options are
    /// validated (Locked mode requires an <see cref="AspNetConfigForgeOptions.OnSave"/>
    /// handler) and a fresh <see cref="IConfigForgeHostState"/> is built from them when
    /// none is registered. To also run the directory watcher hosted service, register
    /// services with
    /// <see cref="ServiceCollectionExtensions.AddConfigForge(IServiceCollection, System.Action{AspNetConfigForgeOptions})"/>
    /// and call the parameterless <see cref="UseConfigForge(IApplicationBuilder)"/> instead.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// <see cref="ConfigForgeMode.Locked"/> mode is selected without an
    /// <see cref="AspNetConfigForgeOptions.OnSave"/> handler, or <paramref name="app"/>
    /// is not an <see cref="IEndpointRouteBuilder"/>.
    /// </exception>
    public static IApplicationBuilder UseConfigForge(
        this IApplicationBuilder app,
        Action<AspNetConfigForgeOptions> configure
    )
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new AspNetConfigForgeOptions();
        configure(options);
        ServiceCollectionExtensions.ValidateOptions(options);

        _ =
            app.ApplicationServices.GetService<IConfigForgeHostState>()
            ?? throw new InvalidOperationException(
                "UseConfigForge(configure) still requires AddConfigForge to have been "
                    + "called in ConfigureServices so the interactive host can resolve its state."
            );

        return MapUi(app);
    }

    private static IApplicationBuilder MapUi(IApplicationBuilder app)
    {
        if (app is not IEndpointRouteBuilder endpoints)
        {
            throw new InvalidOperationException(
                "UseConfigForge hosts interactive Blazor Server and requires an endpoint "
                    + "route builder. Call it on the WebApplication returned by "
                    + "WebApplication.CreateBuilder(...).Build()."
            );
        }

        app.UseRouting();
        app.UseStaticFiles();
        app.UseAntiforgery();
        endpoints.MapStaticAssets();

        endpoints.MapRazorComponents<Host>().AddInteractiveServerRenderMode();

        return app;
    }
}
