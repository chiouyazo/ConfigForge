using ConfigForge.Abstractions;

namespace ConfigForge.AspNet.Endpoints;

/// <summary>
/// <see cref="IActionContext"/> implementation for a headless HTTP call: no Blazor
/// circuit, no dirty tracking, no UI state. Field reads come from a document loaded
/// fresh for the request (or a loader-backed collection's entries, when the acting
/// entry belongs to one); toasts are recorded for the response instead of shown.
/// </summary>
internal sealed class HttpActionContext : IActionContext
{
    private readonly ConfigDocument _document;
    private readonly string? _collectionKey;
    private readonly IReadOnlyList<ConfigDocument>? _collectionEntries;

    /// <summary>Creates an action context over a request-scoped document.</summary>
    /// <param name="document">The document loaded for the request.</param>
    /// <param name="services">The request's service provider.</param>
    /// <param name="cancellationToken">The request's abort token.</param>
    /// <param name="currentFieldKey">The acting entry's path, or empty for a global action.</param>
    /// <param name="collectionKey">
    /// The map field key of the acting entry's collection, when it is loader-backed.
    /// </param>
    /// <param name="collectionEntries">
    /// The collection's freshly loaded entries, when it is loader-backed.
    /// </param>
    public HttpActionContext(
        ConfigDocument document,
        IServiceProvider services,
        CancellationToken cancellationToken,
        string currentFieldKey = "",
        string? collectionKey = null,
        IReadOnlyList<ConfigDocument>? collectionEntries = null
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(services);

        _document = document;
        Services = services;
        CancellationToken = cancellationToken;
        CurrentFieldKey = currentFieldKey;
        _collectionKey = collectionKey;
        _collectionEntries = collectionEntries;
    }

    /// <summary>The toasts the handler raised, in order.</summary>
    public List<(string Message, ToastSeverity Severity)> Toasts { get; } = [];

    /// <inheritdoc />
    public string this[string fieldKey] => Resolve(fieldKey);

    /// <inheritdoc />
    public string CurrentFieldKey { get; }

    /// <inheritdoc />
    public IServiceProvider Services { get; }

    /// <inheritdoc />
    public CancellationToken CancellationToken { get; }

    /// <inheritdoc />
    public Task ShowToastAsync(string message, ToastSeverity severity)
    {
        Toasts.Add((message, severity));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetFieldValueAsync(string fieldKey, object? value) => Task.CompletedTask;

    /// <inheritdoc />
    public Task SetFieldOptionsAsync(string fieldKey, IReadOnlyList<SelectOption> options) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task SetFieldLoadingAsync(string fieldKey, bool loading) => Task.CompletedTask;

    /// <inheritdoc />
    public Task SetFieldEnabledAsync(string fieldKey, bool enabled) => Task.CompletedTask;

    private string Resolve(string fieldKey)
    {
        if (
            _collectionKey is { Length: > 0 } collectionKey
            && _collectionEntries is { } entries
            && fieldKey.StartsWith(collectionKey + "/", StringComparison.Ordinal)
        )
        {
            string remainder = fieldKey[(collectionKey.Length + 1)..];
            int separatorIndex = remainder.IndexOf('/', StringComparison.Ordinal);
            string indexSegment = separatorIndex < 0 ? remainder : remainder[..separatorIndex];
            string relative = separatorIndex < 0 ? string.Empty : remainder[(separatorIndex + 1)..];

            if (
                int.TryParse(indexSegment, out int index)
                && index >= 0
                && index < entries.Count
                && relative.Length > 0
            )
            {
                return entries[index].GetString(relative);
            }

            return string.Empty;
        }

        return _document.GetString(fieldKey);
    }
}
