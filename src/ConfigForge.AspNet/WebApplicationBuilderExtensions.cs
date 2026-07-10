using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace ConfigForge.AspNet;

/// <summary>
/// <see cref="WebApplicationBuilder"/> helpers for hosting ConfigForge.
/// </summary>
public static class WebApplicationBuilderExtensions
{
    /// <summary>
    /// Registers ConfigForge and ensures Blazor's static web assets resolve regardless
    /// of the hosting environment.
    /// </summary>
    /// <remarks>
    /// <see cref="WebApplication.CreateBuilder(string[])"/> only enables static web
    /// assets in the Development environment, so a non-published run (for example
    /// <c>dotnet run</c> in Production) serves no <c>_framework</c>/<c>_content</c>
    /// assets: the interactive Server circuit never starts and component CSS is missing.
    /// This overload calls <c>UseStaticWebAssets()</c> so the interactive editor works in
    /// that case too. It is a no-op when the assets manifest is absent (a published app
    /// already serves them).
    /// </remarks>
    /// <param name="builder">The web application builder.</param>
    /// <param name="configure">A delegate that populates the options.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public static WebApplicationBuilder AddConfigForge(
        this WebApplicationBuilder builder,
        Action<AspNetConfigForgeOptions> configure
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.WebHost.UseStaticWebAssets();
        builder.Services.AddConfigForge(configure);
        return builder;
    }
}
