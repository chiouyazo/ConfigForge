using ConfigForge.Abstractions;
using ConfigForge.AspNet;
using ConfigForge.Core.Documents;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ConfigForge.AspNet.Tests;

public sealed class AddConfigForgeTests
{
    [Fact]
    public void AddConfigForge_LockedModeWithoutOnSave_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddConfigForge(options => options.Mode = ConfigForgeMode.Locked)
        );
    }

    [Fact]
    public void AddConfigForge_LockedModeWithOnSave_Succeeds()
    {
        var services = new ServiceCollection();

        IServiceCollection result = services.AddConfigForge(options =>
        {
            options.Mode = ConfigForgeMode.Locked;
            options.OnSave = (_, _) => Task.CompletedTask;
        });

        Assert.Same(services, result);
    }

    [Fact]
    public void AddConfigForge_OpenMode_ResolvesCoreServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddConfigForge(_ => { });

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IConfigDocumentEngine>());
        Assert.NotNull(provider.GetRequiredService<IThemeProvider>());
        Assert.NotNull(provider.GetRequiredService<AspNetConfigForgeOptions>());
        Assert.NotNull(provider.GetRequiredService<IConfigForgeHostState>());
    }

    /// <summary>
    /// A custom <see cref="AspNetConfigForgeOptions.ThemeProvider"/> must win DI
    /// resolution as <see cref="IThemeProvider"/>. AddConfigForge registers the
    /// host theme provider before AddConfigForgeBlazor, whose fallback
    /// <c>TryAddSingleton&lt;IThemeProvider, DefaultThemeProvider&gt;()</c> then backs off.
    /// </summary>
    [Fact]
    public void AddConfigForge_ConfiguredThemeProvider_ResolvedAsIThemeProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var customTheme = new CustomThemeProvider();

        services.AddConfigForge(options => options.ThemeProvider = customTheme);

        using ServiceProvider provider = services.BuildServiceProvider();

        IThemeProvider resolved = provider.GetRequiredService<IThemeProvider>();
        Assert.Same(customTheme, resolved);
    }

    private sealed class CustomThemeProvider : IThemeProvider
    {
        public ThemeDefinition GetTheme() => new();
    }
}
