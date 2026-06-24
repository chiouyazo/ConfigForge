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
}
