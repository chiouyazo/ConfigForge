using System.Reflection;
using System.Runtime.Loader;
using ConfigForge.Abstractions;
using Serilog;

namespace ConfigForge.Core.Plugins;

/// <summary>
/// Loads <see cref="IPlugin"/> implementations from assemblies or directories
/// into a <see cref="PluginRegistry"/>, each in its own collectible
/// <see cref="AssemblyLoadContext"/>.
/// </summary>
public sealed class PluginLoader : IPluginLoader
{
    private readonly PluginRegistry _registry;
    private readonly ILogger _logger;

    /// <summary>Initializes a new loader targeting the supplied registry.</summary>
    /// <param name="registry">The registry that receives plugin registrations.</param>
    /// <param name="logger">An optional Serilog logger; defaults to the static <see cref="Log.Logger"/>.</param>
    public PluginLoader(PluginRegistry registry, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
        _logger = logger ?? Log.Logger;
    }

    /// <inheritdoc />
    public Task LoadFromDirectoryAsync(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(directoryPath);

        if (!Directory.Exists(directoryPath))
        {
            _logger.Warning(
                "Plugin directory {Directory} does not exist; nothing loaded.",
                directoryPath
            );
            return Task.CompletedTask;
        }

        return LoadDirectoryCoreAsync(directoryPath);
    }

    /// <inheritdoc />
    public Task LoadAssemblyAsync(string assemblyPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(assemblyPath);

        if (!File.Exists(assemblyPath))
        {
            _logger.Warning("Plugin assembly {Assembly} does not exist; skipped.", assemblyPath);
            return Task.CompletedTask;
        }

        return Task.Run(() => LoadAssemblyCore(assemblyPath));
    }

    /// <inheritdoc />
    public void LoadEmbedded(IPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        plugin.Register(_registry);
        _registry.TrackPlugin(plugin.Id);
        _logger.Information(
            "Registered embedded plugin {PluginId} ({DisplayName}).",
            plugin.Id,
            plugin.DisplayName
        );
    }

    private async Task LoadDirectoryCoreAsync(string directoryPath)
    {
        foreach (string dll in Directory.EnumerateFiles(directoryPath, "*.dll"))
        {
            await LoadAssemblyAsync(dll).ConfigureAwait(false);
        }
    }

    private void LoadAssemblyCore(string assemblyPath)
    {
        string contextName = Path.GetFileNameWithoutExtension(assemblyPath);
        AssemblyLoadContext context = new(contextName, isCollectible: true);

        try
        {
            Assembly assembly = context.LoadFromAssemblyPath(Path.GetFullPath(assemblyPath));
            int loaded = 0;

            foreach (Type type in assembly.GetTypes())
            {
                if (!typeof(IPlugin).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface)
                {
                    continue;
                }

                if (Activator.CreateInstance(type) is not IPlugin plugin)
                {
                    continue;
                }

                plugin.Register(_registry);
                _registry.TrackPlugin(plugin.Id);
                loaded++;
                _logger.Information(
                    "Loaded plugin {PluginId} from {Assembly}.",
                    plugin.Id,
                    assemblyPath
                );
            }

            if (loaded == 0)
            {
                _logger.Warning("No IPlugin implementations found in {Assembly}.", assemblyPath);
            }
        }
        catch (BadImageFormatException ex)
        {
            _logger.Warning(ex, "{Assembly} is not a managed assembly; skipped.", assemblyPath);
        }
        catch (ReflectionTypeLoadException ex)
        {
            _logger.Error(ex, "Failed to load types from {Assembly}.", assemblyPath);
        }
    }
}
