namespace ConfigForge.Abstractions;

/// <summary>
/// Provided to every action and loader handler at invocation time.
/// Gives read access to the current field values and write access to the UI state.
/// </summary>
public interface IActionContext
{
    /// <summary>
    /// Gets the current string representation of a field by its JSON Schema property key.
    /// Returns an empty string if the field does not exist or has no value.
    /// </summary>
    /// <param name="fieldKey">The JSON Schema property key of the field.</param>
    string this[string fieldKey] { get; }

    /// <summary>Displays a toast notification to the user.</summary>
    /// <param name="message">The message to display.</param>
    /// <param name="severity">The severity of the notification.</param>
    Task ShowToastAsync(string message, ToastSeverity severity);

    /// <summary>Programmatically sets the value of a field.</summary>
    /// <param name="fieldKey">The JSON Schema property key of the field.</param>
    /// <param name="value">The value to assign.</param>
    Task SetFieldValueAsync(string fieldKey, object? value);

    /// <summary>
    /// Replaces the available options of a select field at runtime.
    /// Typically called from a loader after fetching remote data.
    /// </summary>
    /// <param name="fieldKey">The JSON Schema property key of the field.</param>
    /// <param name="options">The new set of options.</param>
    Task SetFieldOptionsAsync(string fieldKey, IReadOnlyList<SelectOption> options);

    /// <summary>Sets the loading spinner state on a field.</summary>
    /// <param name="fieldKey">The JSON Schema property key of the field.</param>
    /// <param name="loading">Whether the field should show a loading spinner.</param>
    Task SetFieldLoadingAsync(string fieldKey, bool loading);

    /// <summary>Sets the enabled or disabled state of a field.</summary>
    /// <param name="fieldKey">The JSON Schema property key of the field.</param>
    /// <param name="enabled">Whether the field should be enabled.</param>
    Task SetFieldEnabledAsync(string fieldKey, bool enabled);

    /// <summary>
    /// Access to the host service provider for resolving registered dependencies.
    /// </summary>
    IServiceProvider Services { get; }

    /// <summary>
    /// Cancelled if the user navigates away from the current category while an action is in progress.
    /// </summary>
    CancellationToken CancellationToken { get; }
}
