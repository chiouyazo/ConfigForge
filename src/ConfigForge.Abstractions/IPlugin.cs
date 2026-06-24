namespace ConfigForge.Abstractions;

/// <summary>
/// Entry point for a ConfigForge plugin.
/// Implement this interface and reference ConfigForge.Abstractions to create a plugin.
/// </summary>
public interface IPlugin
{
    /// <summary>
    /// Unique identifier for this plugin. Must match the pluginId declared in the x-cf extension.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Human-readable name shown in diagnostic output and the open-mode plugin list.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Called once at startup. Register all actions, loaders, validators, and custom controls here.
    /// </summary>
    /// <param name="registry">The registry that receives the plugin's registrations.</param>
    void Register(IPluginRegistry registry);
}
