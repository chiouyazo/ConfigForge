namespace ConfigForge.Core.Plugins;

/// <summary>The outcome of one <see cref="ICapabilityLoader.Load"/> call.</summary>
public enum CapabilityLoadStatus
{
    /// <summary>The assembly loaded and its capability interfaces were enumerated.</summary>
    Loaded,

    /// <summary>
    /// The assembly file did not exist at the given path. Any previously loaded capabilities
    /// remain available; nothing was reset.
    /// </summary>
    AssemblyNotFound,

    /// <summary>
    /// The file exists but could not be loaded as a managed assembly. Any previously loaded
    /// capabilities remain available; nothing was reset.
    /// </summary>
    LoadFailed,
}
