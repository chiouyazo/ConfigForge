using ConfigForge.Abstractions;

namespace ConfigForge.Blazor.Services;

/// <summary>
/// Dispatches plugin action and loader handlers, resolving them from the plugin
/// catalog and shielding the UI from handler exceptions.
/// </summary>
public interface IActionDispatcher
{
    /// <summary>
    /// Invokes the action handler registered under <paramref name="actionId"/>.
    /// A missing handler or a handler failure is surfaced as a toast and logged;
    /// it never propagates to the caller.
    /// </summary>
    /// <param name="actionId">The action identifier to dispatch.</param>
    /// <param name="context">The action context handed to the handler.</param>
    /// <returns>A task that completes when the handler has run (or been skipped).</returns>
    Task DispatchActionAsync(string actionId, IActionContext context);

    /// <summary>
    /// Invokes the loader handler registered under <paramref name="loaderId"/> and
    /// returns its options. A missing handler, cancellation, or failure yields an
    /// empty list rather than throwing.
    /// </summary>
    /// <param name="loaderId">The loader identifier to dispatch.</param>
    /// <param name="context">The action context handed to the handler.</param>
    /// <returns>The loaded options, or an empty list on any failure.</returns>
    Task<IReadOnlyList<SelectOption>> DispatchLoaderAsync(string loaderId, IActionContext context);
}
