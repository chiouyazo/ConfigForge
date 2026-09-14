using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
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
    private readonly ConcurrentDictionary<
        string,
        Func<IActionContext, CancellationToken, Task<IReadOnlyList<ConfigDocument>>>
    > _collectionLoaders = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _pluginIds = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<
        string,
        Func<IReadOnlyList<object?>, CancellationToken, Task<object?>>
    > _capabilityActions = new(StringComparer.Ordinal);

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
    public void RegisterCollectionLoader(
        string collectionKey,
        Func<IActionContext, CancellationToken, Task<IReadOnlyList<ConfigDocument>>> handler
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(collectionKey);
        ArgumentNullException.ThrowIfNull(handler);
        _collectionLoaders[collectionKey] = handler;
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

    /// <inheritdoc />
    public bool TryGetCollectionLoader(
        string collectionKey,
        out Func<IActionContext, CancellationToken, Task<IReadOnlyList<ConfigDocument>>>? handler
    )
    {
        bool found = _collectionLoaders.TryGetValue(
            collectionKey,
            out Func<IActionContext, CancellationToken, Task<IReadOnlyList<ConfigDocument>>>? value
        );
        handler = value;
        return found;
    }

    /// <inheritdoc />
    public void RegisterCapabilityImplementation<TContract>(TContract implementation)
        where TContract : class
    {
        ArgumentNullException.ThrowIfNull(implementation);

        Type contractType = typeof(TContract);
        foreach (MethodInfo method in contractType.GetMethods())
        {
            string actionId = $"{contractType.FullName}.{method.Name}";
            _capabilityActions[actionId] = BuildInvoker(implementation, method);
        }
    }

    private static Func<IReadOnlyList<object?>, CancellationToken, Task<object?>> BuildInvoker(
        object implementation,
        MethodInfo method
    )
    {
        ParameterInfo[] parameters = method.GetParameters();
        bool takesCancellationToken =
            parameters.Length > 0 && parameters[^1].ParameterType == typeof(CancellationToken);
        int wireParameterCount = takesCancellationToken ? parameters.Length - 1 : parameters.Length;

        return async (wireArgs, cancellationToken) =>
        {
            object?[] callArgs = new object?[parameters.Length];
            for (int i = 0; i < wireParameterCount; i++)
            {
                callArgs[i] = ConvertArgument(
                    i < wireArgs.Count ? wireArgs[i] : null,
                    parameters[i].ParameterType
                );
            }

            if (takesCancellationToken)
            {
                callArgs[^1] = cancellationToken;
            }

            object? invocationResult = method.Invoke(implementation, callArgs);
            return invocationResult switch
            {
                Task<object?> boxedTask => await boxedTask,
                Task task => await AwaitAndUnwrapAsync(task),
                var other => other,
            };
        };
    }

    private static async Task<object?> AwaitAndUnwrapAsync(Task task)
    {
        await task;

        Type taskType = task.GetType();
        if (!taskType.IsGenericType)
        {
            return null;
        }

        PropertyInfo resultProperty = taskType.GetProperty(nameof(Task<object>.Result))!;
        return resultProperty.GetValue(task);
    }

    private static object? ConvertArgument(object? wireValue, Type targetType) =>
        wireValue switch
        {
            null => null,
            JsonElement element => element.Deserialize(targetType),
            var value when targetType.IsInstanceOfType(value) => value,
            var value => JsonSerializer.Deserialize(JsonSerializer.Serialize(value), targetType),
        };

    /// <inheritdoc />
    public bool TryGetCapabilityAction(
        string actionId,
        out Func<IReadOnlyList<object?>, CancellationToken, Task<object?>>? handler
    )
    {
        bool found = _capabilityActions.TryGetValue(
            actionId,
            out Func<IReadOnlyList<object?>, CancellationToken, Task<object?>>? value
        );
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
