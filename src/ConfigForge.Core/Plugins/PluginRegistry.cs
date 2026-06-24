using System.Collections.Concurrent;
using ConfigForge.Abstractions;

namespace ConfigForge.Core.Plugins;

/// <summary>
/// The host plugin registry: the write side consumed by <see cref="IPlugin"/>
/// implementations and the read side (<see cref="IPluginCatalog"/>) consumed by
/// the action dispatcher and UI. Registrations are last-wins (no throw on
/// duplicate id).
/// </summary>
public sealed class PluginRegistry : IPluginRegistry, IPluginCatalog
{
    private readonly ConcurrentDictionary<string, Func<IActionContext, Task>> _actions = new(
        StringComparer.Ordinal
    );
    private readonly ConcurrentDictionary<
        string,
        Func<IActionContext, Task<IReadOnlyList<SelectOption>>>
    > _loaders = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Type> _controls = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Func<object?, ValidationResult>> _validators =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _pluginIds = new(StringComparer.Ordinal);

    /// <inheritdoc />
#pragma warning disable S2365 // Intentional snapshot: the backing store is concurrent and the contract is a property.
    public IReadOnlyCollection<string> RegisteredPluginIds => _pluginIds.Keys.ToArray();
#pragma warning restore S2365

    /// <inheritdoc />
    public void RegisterAction(string actionId, Func<IActionContext, Task> handler)
    {
        ArgumentException.ThrowIfNullOrEmpty(actionId);
        ArgumentNullException.ThrowIfNull(handler);
        _actions[actionId] = handler;
    }

    /// <inheritdoc />
    public void RegisterLoader(
        string loaderId,
        Func<IActionContext, Task<IReadOnlyList<SelectOption>>> handler
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(loaderId);
        ArgumentNullException.ThrowIfNull(handler);
        _loaders[loaderId] = handler;
    }

    /// <inheritdoc />
    public void RegisterControl(string typeId, Type componentType)
    {
        ArgumentException.ThrowIfNullOrEmpty(typeId);
        ArgumentNullException.ThrowIfNull(componentType);

        bool implementsControl = Array.Exists(
            componentType.GetInterfaces(),
            i => string.Equals(i.Name, "IConfigControl", StringComparison.Ordinal)
        );
        if (!implementsControl)
        {
            throw new ArgumentException(
                $"Type '{componentType.FullName}' does not implement IConfigControl.",
                nameof(componentType)
            );
        }

        _controls[typeId] = componentType;
    }

    /// <inheritdoc />
    public void RegisterValidator(string validatorId, Func<object?, ValidationResult> handler)
    {
        ArgumentException.ThrowIfNullOrEmpty(validatorId);
        ArgumentNullException.ThrowIfNull(handler);
        _validators[validatorId] = handler;
    }

    /// <inheritdoc />
    public bool TryGetAction(string id, out Func<IActionContext, Task>? handler)
    {
        bool found = _actions.TryGetValue(id, out Func<IActionContext, Task>? value);
        handler = value;
        return found;
    }

    /// <inheritdoc />
    public bool TryGetLoader(
        string id,
        out Func<IActionContext, Task<IReadOnlyList<SelectOption>>>? handler
    )
    {
        bool found = _loaders.TryGetValue(
            id,
            out Func<IActionContext, Task<IReadOnlyList<SelectOption>>>? value
        );
        handler = value;
        return found;
    }

    /// <inheritdoc />
    public bool TryGetControl(string typeId, out Type? componentType)
    {
        bool found = _controls.TryGetValue(typeId, out Type? value);
        componentType = value;
        return found;
    }

    /// <inheritdoc />
    public bool TryGetValidator(string id, out Func<object?, ValidationResult>? handler)
    {
        bool found = _validators.TryGetValue(id, out Func<object?, ValidationResult>? value);
        handler = value;
        return found;
    }

    /// <summary>Records that a plugin with the given id has been loaded.</summary>
    /// <param name="pluginId">The plugin identifier to record.</param>
    public void TrackPlugin(string pluginId)
    {
        ArgumentException.ThrowIfNullOrEmpty(pluginId);
        _pluginIds[pluginId] = 0;
    }
}
