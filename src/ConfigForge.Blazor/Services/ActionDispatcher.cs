using ConfigForge.Abstractions;
using ConfigForge.Core.Plugins;
using Microsoft.Extensions.Logging;

namespace ConfigForge.Blazor.Services;

/// <summary>
/// Default <see cref="IActionDispatcher"/>: resolves handlers from the
/// <see cref="IPluginCatalog"/> and runs them defensively, turning every failure
/// mode into a logged event plus a user-facing toast.
/// </summary>
public sealed partial class ActionDispatcher : IActionDispatcher
{
    private readonly IPluginCatalog _registry;
    private readonly ILogger<ActionDispatcher> _logger;

    /// <summary>Creates a dispatcher over the given plugin catalog and logger.</summary>
    /// <param name="registry">The read side of the plugin registry.</param>
    /// <param name="logger">The logger for diagnostic output.</param>
    public ActionDispatcher(IPluginCatalog registry, ILogger<ActionDispatcher> logger)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(logger);

        _registry = registry;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task DispatchActionAsync(string actionId, IActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (
            !_registry.TryGetAction(actionId, out Func<IActionContext, Task>? handler)
            || handler is null
        )
        {
            LogActionNotRegistered(actionId);
            await context.ShowToastAsync(
                $"Action '{actionId}' is not registered",
                ToastSeverity.Warning
            );
            return;
        }

        try
        {
            await handler(context);
            LogActionInvoked(actionId);
        }
        catch (OperationCanceledException)
        {
            LogActionCancelled(actionId);
        }
#pragma warning disable CA1031 // A plugin action is untrusted code; the host must not crash on any failure it raises.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogActionFailed(ex, actionId);
            await context.ShowToastAsync("An unexpected error occurred", ToastSeverity.Danger);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SelectOption>> DispatchLoaderAsync(
        string loaderId,
        IActionContext context
    )
    {
        ArgumentNullException.ThrowIfNull(context);

        if (
            !_registry.TryGetLoader(
                loaderId,
                out Func<IActionContext, Task<IReadOnlyList<SelectOption>>>? handler
            ) || handler is null
        )
        {
            LogLoaderNotRegistered(loaderId);
            return [];
        }

        try
        {
            IReadOnlyList<SelectOption> options = await handler(context);
            LogLoaderInvoked(loaderId, options.Count);
            return options;
        }
        catch (OperationCanceledException)
        {
            LogLoaderCancelled(loaderId);
            return [];
        }
#pragma warning disable CA1031 // A plugin loader is untrusted code; the host must not crash on any failure it raises.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogLoaderFailed(ex, loaderId);
            await context.ShowToastAsync("Failed to load options", ToastSeverity.Danger);
            return [];
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Action '{ActionId}' is not registered."
    )]
    partial void LogActionNotRegistered(string actionId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Action '{ActionId}' was cancelled."
    )]
    partial void LogActionCancelled(string actionId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Action '{ActionId}' threw an unexpected exception."
    )]
    partial void LogActionFailed(Exception exception, string actionId);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message = "Loader '{LoaderId}' is not registered."
    )]
    partial void LogLoaderNotRegistered(string loaderId);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "Loader '{LoaderId}' was cancelled."
    )]
    partial void LogLoaderCancelled(string loaderId);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Error,
        Message = "Loader '{LoaderId}' threw an unexpected exception."
    )]
    partial void LogLoaderFailed(Exception exception, string loaderId);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Information,
        Message = "Action invoked {ActionId}"
    )]
    partial void LogActionInvoked(string actionId);

    [LoggerMessage(
        EventId = 8,
        Level = LogLevel.Information,
        Message = "Loader invoked {LoaderId} returned {OptionCount} options"
    )]
    partial void LogLoaderInvoked(string loaderId, int optionCount);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConfigDocument>> DispatchCollectionLoaderAsync(
        string collectionKey,
        IActionContext context
    )
    {
        ArgumentNullException.ThrowIfNull(context);

        if (
            !_registry.TryGetCollectionLoader(
                collectionKey,
                out Func<
                    IActionContext,
                    CancellationToken,
                    Task<IReadOnlyList<ConfigDocument>>
                >? handler
            ) || handler is null
        )
        {
            LogCollectionLoaderNotRegistered(collectionKey);
            return [];
        }

        try
        {
            IReadOnlyList<ConfigDocument> entries = await handler(
                context,
                context.CancellationToken
            );
            LogCollectionLoaderInvoked(collectionKey, entries.Count);
            return entries;
        }
        catch (OperationCanceledException)
        {
            LogCollectionLoaderCancelled(collectionKey);
            return [];
        }
#pragma warning disable CA1031 // A plugin collection loader is untrusted code; the host must not crash on any failure it raises.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogCollectionLoaderFailed(ex, collectionKey);
            await context.ShowToastAsync("Failed to load entries", ToastSeverity.Danger);
            return [];
        }
    }

    [LoggerMessage(
        EventId = 9,
        Level = LogLevel.Warning,
        Message = "Collection loader for '{CollectionKey}' is not registered."
    )]
    partial void LogCollectionLoaderNotRegistered(string collectionKey);

    [LoggerMessage(
        EventId = 10,
        Level = LogLevel.Information,
        Message = "Collection loader for '{CollectionKey}' was cancelled."
    )]
    partial void LogCollectionLoaderCancelled(string collectionKey);

    [LoggerMessage(
        EventId = 11,
        Level = LogLevel.Error,
        Message = "Collection loader for '{CollectionKey}' threw an unexpected exception."
    )]
    partial void LogCollectionLoaderFailed(Exception exception, string collectionKey);

    [LoggerMessage(
        EventId = 12,
        Level = LogLevel.Information,
        Message = "Collection loader invoked for '{CollectionKey}' returned {EntryCount} entries"
    )]
    partial void LogCollectionLoaderInvoked(string collectionKey, int entryCount);
}
