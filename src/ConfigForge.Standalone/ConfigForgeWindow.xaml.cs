using System.IO;
using System.Windows;
using ConfigForge.Abstractions;
using ConfigForge.Blazor.Components;
using ConfigForge.Blazor.Services;
using ConfigForge.Core;
using ConfigForge.Core.Documents;
using ConfigForge.Core.Plugins;
using ConfigForge.Core.Schema;
using ConfigForge.Generated;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace ConfigForge.Standalone;

/// <summary>
/// The main application window. Hosts the ConfigForge Blazor UI in a
/// <c>BlazorWebView</c> and applies the resolved host options.
/// </summary>
public sealed partial class ConfigForgeWindow : Window
{
    /// <summary>
    /// Initializes the window, builds the service provider, mounts the
    /// <see cref="ConfigForgeShell"/> root component, and applies the host options.
    /// </summary>
    public ConfigForgeWindow()
    {
        InitializeComponent();

        ServiceProvider provider = BuildServices();

        BlazorWebView.Services = provider;
        BlazorWebView.RootComponents.Add(BuildRootComponent(provider));

        var options = provider.GetRequiredService<StandaloneConfigForgeOptions>();
        Title = options.ApplicationTitle;
        Width = options.InitialWidth;
        Height = options.InitialHeight;
    }

    /// <summary>
    /// Builds the shell root component. When an embedded schema is present it is
    /// parsed, seeded with an example document, and mounted in
    /// <see cref="ConfigForgeMode.Locked"/> mode. Otherwise the shell is mounted
    /// without parameters so the host still launches.
    /// </summary>
    private static RootComponent BuildRootComponent(IServiceProvider provider)
    {
        string? schemaJson = EmbeddedOptionsLoader.LoadSchemaJson();
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            Log.Warning("No embedded schema found; mounting the shell without a schema.");
            return new RootComponent
            {
                Selector = "#app",
                ComponentType = typeof(ConfigForgeShell),
            };
        }

        var parser = provider.GetRequiredService<IJsonFormsSchemaParser>();
        var generator = provider.GetRequiredService<IConfigDocumentGenerator>();

        ConfigSchema schema = parser.Parse(schemaJson);
        ConfigDocument document = generator.GenerateExample(schema);

        Log.Information(
            "Embedded schema loaded: {SchemaId} {Version} categories={CategoryCount}",
            schema.Id,
            schema.Version,
            schema.Categories.Count
        );

        return new RootComponent
        {
            Selector = "#app",
            ComponentType = typeof(ConfigForgeShell),
            Parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["Schema"] = schema,
                ["Document"] = document,
                ["Mode"] = ConfigForgeMode.Locked,
                ["ShowGenerateButton"] = false,
            },
        };
    }

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        services.AddWpfBlazorWebView();
        services.AddConfigForgeCore();
        services.AddConfigForgeBlazor();
        services.AddSingleton(EmbeddedOptionsLoader.Load());
        services.AddLogging(builder => builder.AddSerilog());

        ServiceProvider provider = services.BuildServiceProvider();

        GeneratedPluginRegistrar.RegisterAll(provider.GetRequiredService<IPluginRegistry>());

        var loader = provider.GetRequiredService<IPluginLoader>();
        string pluginDir = Path.Combine(AppContext.BaseDirectory, "plugins");
        Log.Information("Loading external plugins from trusted path {PluginDir}", pluginDir);

        loader.LoadFromDirectoryAsync(pluginDir).GetAwaiter().GetResult();

        return provider;
    }
}
