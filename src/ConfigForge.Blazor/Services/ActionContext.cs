using ConfigForge.Abstractions;

namespace ConfigForge.Blazor.Services;

/// <summary>
/// <see cref="IActionContext"/> implementation backed by an
/// <see cref="EditingSession"/>. Field reads come from the live document; field
/// and toast writes are routed back into the session so the UI re-renders.
/// </summary>
public sealed class ActionContext : IActionContext
{
    private readonly EditingSession _session;

    /// <summary>Creates an action context over the given session and services.</summary>
    /// <param name="session">The editing session providing state.</param>
    /// <param name="services">The host service provider exposed to handlers.</param>
    public ActionContext(
        EditingSession session,
        IServiceProvider services,
        string? currentFieldKey = null
    )
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(services);

        _session = session;
        Services = services;
        CurrentFieldKey = currentFieldKey ?? string.Empty;
    }

    /// <inheritdoc />
    public string this[string fieldKey] => _session.Document.GetString(fieldKey);

    /// <inheritdoc />
    public string CurrentFieldKey { get; }

    /// <inheritdoc />
    public IServiceProvider Services { get; }

    /// <inheritdoc />
    public CancellationToken CancellationToken => _session.CategoryCancellationToken;

    /// <inheritdoc />
    public Task ShowToastAsync(string message, ToastSeverity severity)
    {
        _session.EnqueueToast(message, severity);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetFieldValueAsync(string fieldKey, object? value)
    {
        _session.SetFieldValue(fieldKey, value);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetFieldOptionsAsync(string fieldKey, IReadOnlyList<SelectOption> options)
    {
        _session.SetFieldOptions(fieldKey, options);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetFieldLoadingAsync(string fieldKey, bool loading)
    {
        _session.SetFieldLoading(fieldKey, loading);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetFieldEnabledAsync(string fieldKey, bool enabled)
    {
        _session.SetFieldEnabled(fieldKey, enabled);
        return Task.CompletedTask;
    }
}
