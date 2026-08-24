using ConfigForge.Abstractions;

namespace ConfigForge.Core.Plugins;

/// <summary>Discovers and loads plugins into the host registry.</summary>
public interface IPluginLoader
{
    /// <summary>Loads every plugin assembly (<c>*.dll</c>) in a directory.</summary>
    /// <param name="directoryPath">The directory to scan.</param>
    /// <returns>A task that completes when all assemblies are loaded.</returns>
    Task LoadFromDirectoryAsync(string directoryPath);

    /// <summary>
    /// Loads every plugin assembly (<c>*.dll</c>) in a directory synchronously. Assembly loading is
    /// CPU-bound, so a caller with no async context (e.g. application startup) uses this instead of
    /// blocking on the async overload.
    /// </summary>
    /// <param name="directoryPath">The directory to scan.</param>
    void LoadFromDirectory(string directoryPath);

    /// <summary>Loads all plugins from a single assembly file.</summary>
    /// <param name="assemblyPath">The path to the plugin assembly.</param>
    /// <returns>A task that completes when the assembly is loaded.</returns>
    Task LoadAssemblyAsync(string assemblyPath);

    /// <summary>Registers an already-instantiated plugin directly.</summary>
    /// <param name="plugin">The plugin instance to register.</param>
    void LoadEmbedded(IPlugin plugin);
}
