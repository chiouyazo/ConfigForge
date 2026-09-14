namespace ConfigForge.Abstractions;

/// <summary>
/// Receives all registrations from an <see cref="IPlugin"/> during startup.
/// </summary>
public interface IPluginRegistry
{
    /// <summary>
    /// Registers a handler for an action button declared in x-cf actions via actionId.
    /// </summary>
    /// <param name="actionId">The action identifier declared in the schema.</param>
    /// <param name="handler">The handler invoked when the action button is pressed.</param>
    void RegisterAction(string actionId, Func<IActionContext, Task> handler);

    /// <summary>
    /// Registers a handler that populates a select field declared via x-cf loaderId.
    /// </summary>
    /// <param name="loaderId">The loader identifier declared in the schema.</param>
    /// <param name="handler">The handler that returns the options to display.</param>
    void RegisterLoader(
        string loaderId,
        Func<IActionContext, Task<IReadOnlyList<SelectOption>>> handler
    );

    /// <summary>
    /// Registers a Blazor component type for a custom control type identifier.
    /// The component must implement <c>IConfigControl</c>.
    /// </summary>
    /// <param name="typeId">The custom control type identifier used in x-cf controls.</param>
    /// <param name="componentType">The Blazor component type implementing the control.</param>
    void RegisterControl(string typeId, Type componentType);

    /// <summary>
    /// Registers a synchronous validator for a field declared via x-cf validatorId.
    /// </summary>
    /// <param name="validatorId">The validator identifier declared in the schema.</param>
    /// <param name="handler">The validator invoked with the current field value.</param>
    void RegisterValidator(string validatorId, Func<object?, ValidationResult> handler);

    /// <summary>
    /// Registers a handler that supplies live, read-only entries for a collection category
    /// declared via x-cf categories[label].collection, instead of the category's entries coming
    /// from the saved document. The category renders read-only (no add/remove/edit) and its
    /// entries are never persisted; RequiresEntry actions still work against the selected entry.
    /// </summary>
    /// <param name="collectionKey">
    /// The map field key named by the category's <c>collection</c> extension.
    /// </param>
    /// <param name="handler">The handler that returns the entries to display.</param>
    void RegisterCollectionLoader(
        string collectionKey,
        Func<IActionContext, CancellationToken, Task<IReadOnlyList<ConfigDocument>>> handler
    );

    /// <summary>
    /// Serves every method of a <c>[CapabilityContract]</c> interface over the same
    /// <c>{prefix}/action/{actionId}</c> endpoint a remote aggregator's capability-widget proxy
    /// calls, so that proxy for <typeparamref name="TContract"/> reaches this real, local
    /// implementation. One entry is registered per method, keyed
    /// <c>{typeof(TContract).FullName}.{MethodName}</c>, matching the action id a proxy call
    /// addresses.
    /// </summary>
    /// <typeparam name="TContract">The capability contract interface.</typeparam>
    /// <param name="implementation">The real implementation to invoke for every call.</param>
    void RegisterCapabilityImplementation<TContract>(TContract implementation)
        where TContract : class;
}
