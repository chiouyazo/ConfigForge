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
        string fullPath = Path.GetFullPath(assemblyPath);
        PluginLoadContext context = new(fullPath);

        try
        {
            Assembly assembly = context.LoadFromAssemblyPath(fullPath);
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
            // The loader exceptions name the missing dependency, which is the actionable detail
            // (e.g. a third-party package the plugin references but did not ship alongside itself).
            string missing = string.Join(
                "; ",
                ex.LoaderExceptions.Where(e => e is not null).Select(e => e!.Message).Distinct()
            );
            _logger.Error(
                ex,
                "Failed to load types from {Assembly}. Missing/unloadable dependencies: {Missing}",
                assemblyPath,
                string.IsNullOrEmpty(missing) ? "(none reported)" : missing
            );
        }
    }

    /// <summary>
    /// A collectible load context for one plugin. Its private dependencies resolve from the
    /// plugin's own folder (via its <c>.deps.json</c>), while the shared ConfigForge contracts —
    /// and any framework assembly — are left to the host's default context so their types keep a
    /// single identity across the host/plugin boundary. Without this, a plugin that references
    /// any package the host doesn't also load fails to load entirely.
    /// </summary>
    private sealed class PluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public PluginLoadContext(string mainAssemblyPath)
            : base(Path.GetFileNameWithoutExtension(mainAssemblyPath), isCollectible: true) =>
            _resolver = new AssemblyDependencyResolver(mainAssemblyPath);

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // Shared contracts must be the host's copy (return null → default context resolves it),
            // otherwise the plugin's IPlugin/IConfigControl would be a different type than the host's.
            if (assemblyName.Name?.StartsWith("ConfigForge.", StringComparison.Ordinal) == true)
            {
                return null;
            }

            string? path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path is null ? null : LoadFromAssemblyPath(path);
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string? path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
        }
    }
}
