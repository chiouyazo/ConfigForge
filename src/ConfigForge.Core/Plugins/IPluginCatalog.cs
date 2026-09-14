using ConfigForge.Abstractions;

namespace ConfigForge.Core.Plugins;

/// <summary>
/// Documented addition: the read side of the plugin registry. Abstractions'
/// <see cref="IPluginRegistry"/> is write-only, but the action dispatcher and UI
/// need to look registrations up, so the host registry also implements this.
/// </summary>
public interface IPluginCatalog
{
    /// <summary>Attempts to resolve a registered action handler.</summary>
    /// <param name="id">The action identifier.</param>
    /// <param name="handler">The handler if found.</param>
    /// <returns>True when an action with the id is registered.</returns>
    bool TryGetAction(string id, out Func<IActionContext, Task>? handler);

    /// <summary>Attempts to resolve a registered loader handler.</summary>
    /// <param name="id">The loader identifier.</param>
    /// <param name="handler">The handler if found.</param>
    /// <returns>True when a loader with the id is registered.</returns>
    bool TryGetLoader(
        string id,
        out Func<IActionContext, Task<IReadOnlyList<SelectOption>>>? handler
    );

    /// <summary>Attempts to resolve a registered custom-control component type.</summary>
    /// <param name="typeId">The control type identifier.</param>
    /// <param name="componentType">The component type if found.</param>
    /// <returns>True when a control with the type id is registered.</returns>
    bool TryGetControl(string typeId, out Type? componentType);

    /// <summary>Attempts to resolve a registered validator.</summary>
    /// <param name="id">The validator identifier.</param>
    /// <param name="handler">The validator if found.</param>
    /// <returns>True when a validator with the id is registered.</returns>
    bool TryGetValidator(string id, out Func<object?, ValidationResult>? handler);

    /// <summary>Attempts to resolve a registered collection loader.</summary>
    /// <param name="collectionKey">The map field key the loader was registered for.</param>
    /// <param name="handler">The handler if found.</param>
    /// <returns>True when a collection loader is registered for the key.</returns>
    bool TryGetCollectionLoader(
        string collectionKey,
        out Func<IActionContext, CancellationToken, Task<IReadOnlyList<ConfigDocument>>>? handler
    );

    /// <summary>The identifiers of all plugins that have been loaded.</summary>
    IReadOnlyCollection<string> RegisteredPluginIds { get; }

    /// <summary>Attempts to resolve a registered capability implementation method.</summary>
    /// <param name="actionId">The action id, <c>{contractFullName}.{methodName}</c>.</param>
    /// <param name="handler">The handler if found.</param>
    /// <returns>True when a capability method with the id is registered.</returns>
    bool TryGetCapabilityAction(
        string actionId,
        out Func<IReadOnlyList<object?>, CancellationToken, Task<object?>>? handler
    );
}
