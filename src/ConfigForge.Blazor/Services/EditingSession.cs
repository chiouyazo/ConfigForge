using ConfigForge.Abstractions;
using ConfigForge.Core.Documents;
using ConfigForge.Core.Plugins;
using ConfigForge.Core.Schema;

namespace ConfigForge.Blazor.Services;

/// <summary>
/// The scoped, mutable state for one editing session: the active schema, the live
/// document, the dirty tracker, the parse result, per-field UI state, and the
/// toast queue. Components subscribe to <see cref="StateChanged"/> to re-render and
/// to <see cref="ToastsChanged"/> for the toast container.
/// </summary>
public sealed class EditingSession : IDisposable
{
    private readonly IDirtyStateTracker _dirtyTracker;
    private readonly IPluginCatalog _pluginCatalog;
    private readonly Dictionary<string, IReadOnlyList<SelectOption>> _fieldOptions = new(
        StringComparer.Ordinal
    );
    private readonly Dictionary<string, bool> _fieldLoading = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _fieldEnabled = new(StringComparer.Ordinal);

    private readonly Dictionary<string, string?> _fieldErrors = new(StringComparer.Ordinal);
    private readonly List<ToastMessage> _toasts = [];

    private CancellationTokenSource _categoryCts = new();
    private bool _disposed;

    /// <summary>Creates a session backed by the supplied dirty-state tracker.</summary>
    /// <param name="dirtyTracker">The dirty-state tracker for this session.</param>
    /// <param name="pluginCatalog">The plugin catalog used to resolve field validators.</param>
    public EditingSession(IDirtyStateTracker dirtyTracker, IPluginCatalog pluginCatalog)
    {
        ArgumentNullException.ThrowIfNull(dirtyTracker);
        ArgumentNullException.ThrowIfNull(pluginCatalog);
        _dirtyTracker = dirtyTracker;
        _pluginCatalog = pluginCatalog;
        _dirtyTracker.DirtyStateChanged += OnDirtyStateChanged;
    }

    /// <summary>Raised whenever session state changes and the UI should re-render.</summary>
    public event EventHandler? StateChanged;

    /// <summary>Raised whenever the toast queue changes.</summary>
    public event EventHandler? ToastsChanged;

    /// <summary>The schema currently being edited, or null before initialization.</summary>
    public ConfigSchema? Schema { get; private set; }

    /// <summary>The live document being edited.</summary>
    public ConfigDocument Document { get; private set; } = new();

    /// <summary>The most recent parse result, or null if the document was generated.</summary>
    public ConfigDocumentParseResult? ParseResult { get; private set; }

    /// <summary>The index of the active category in <see cref="ConfigSchema.Categories"/>.</summary>
    public int ActiveCategoryIndex { get; private set; }

    /// <summary>
    /// The raw JSON the session was seeded with, retained so the malformed-JSON
    /// fallback editor can show the original text for correction.
    /// </summary>
    public string? RawJson { get; private set; }

    /// <summary>True when the document has unsaved edits relative to the baseline.</summary>
    public bool IsDirty => _dirtyTracker.IsDirty;

    /// <summary>The keys whose values differ from the saved baseline.</summary>
    public IReadOnlySet<string> DirtyKeys => _dirtyTracker.DirtyKeys;

    /// <summary>The currently queued toasts.</summary>
    public IReadOnlyList<ToastMessage> Toasts => _toasts;

    /// <summary>
    /// A token cancelled when the user navigates away from the active category,
    /// allowing in-flight actions and loaders to abort.
    /// </summary>
    public CancellationToken CategoryCancellationToken => _categoryCts.Token;

    /// <summary>
    /// Initializes the session with a schema and document, snapshots the document
    /// as the clean baseline, and resets per-field UI state.
    /// </summary>
    /// <param name="schema">The schema to edit against.</param>
    /// <param name="document">The document to edit.</param>
    /// <param name="parseResult">The originating parse result, if any.</param>
    /// <param name="rawJson">The raw JSON the document came from, if any.</param>
    public void Initialize(
        ConfigSchema schema,
        ConfigDocument document,
        ConfigDocumentParseResult? parseResult = null,
        string? rawJson = null
    )
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(document);

        Schema = schema;
        Document = document;
        ParseResult = parseResult;
        RawJson = rawJson;
        ActiveCategoryIndex = 0;

        _fieldOptions.Clear();
        _fieldLoading.Clear();
        _fieldEnabled.Clear();
        _fieldErrors.Clear();

        _dirtyTracker.Snapshot(document);
        _dirtyTracker.Update(document);

        ResetCategoryToken();
        RaiseStateChanged();
    }

    /// <summary>Replaces the live document, refreshing the parse result and dirty state.</summary>
    /// <param name="document">The new document.</param>
    /// <param name="parseResult">The originating parse result, if any.</param>
    public void ReplaceDocument(
        ConfigDocument document,
        ConfigDocumentParseResult? parseResult = null
    )
    {
        ArgumentNullException.ThrowIfNull(document);

        Document = document;
        ParseResult = parseResult;

        _fieldErrors.Clear();
        _dirtyTracker.Update(document);
        RaiseStateChanged();
    }

    /// <summary>Updates the retained raw JSON, e.g. after a re-parse from the editor.</summary>
    /// <param name="rawJson">The raw JSON text.</param>
    public void SetRawJson(string? rawJson) => RawJson = rawJson;

    /// <summary>Activates a category by index and resets the navigation token.</summary>
    /// <param name="index">The category index to activate.</param>
    public void SetActiveCategory(int index)
    {
        if (index == ActiveCategoryIndex)
        {
            return;
        }

        ActiveCategoryIndex = index;
        ResetCategoryToken();
        RaiseStateChanged();
    }

    /// <summary>Reads a field's current raw value.</summary>
    /// <param name="key">The field key.</param>
    /// <returns>The raw value, or null when absent.</returns>
    public object? GetFieldValue(string key) => Document[key];

    /// <summary>
    /// Writes a field value, recomputes dirty state, and notifies subscribers.
    /// </summary>
    /// <param name="key">The field key.</param>
    /// <param name="value">The new value.</param>
    public void SetFieldValue(string key, object? value)
    {
        ArgumentNullException.ThrowIfNull(key);

        Document[key] = value;
        RunFieldValidator(key, value);
        _dirtyTracker.Update(Document);
        RaiseStateChanged();
    }

    /// <summary>
    /// Returns the error message for a field, or null when it is valid. Combines, in
    /// order: a live plugin-validator failure, a required-but-missing parse error, and
    /// a schema-constraint validation error from the parse result.
    /// </summary>
    /// <param name="key">The field key.</param>
    /// <returns>The error message, or null.</returns>
    public string? GetFieldError(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (
            _fieldErrors.TryGetValue(key, out string? validatorMessage)
            && validatorMessage is not null
        )
        {
            return validatorMessage;
        }

        if (ParseResult is { } result)
        {
            if (result.MissingRequiredKeys.Contains(key, StringComparer.Ordinal))
            {
                return "This field is required";
            }

            ValidationError? invalid = result.InvalidValues.FirstOrDefault(e =>
                string.Equals(e.Key, key, StringComparison.Ordinal)
            );
            if (invalid is not null)
            {
                return invalid.Message;
            }
        }

        return null;
    }

    private void RunFieldValidator(string key, object? value)
    {
        if (
            Schema is { } schema
            && schema.Fields.TryGetValue(key, out FieldDefinition? field)
            && field.ValidatorId is { Length: > 0 } validatorId
            && _pluginCatalog.TryGetValidator(
                validatorId,
                out Func<object?, ValidationResult>? validator
            )
            && validator is not null
        )
        {
            ValidationResult result = validator(value);
            _fieldErrors[key] = result.IsValid ? null : result.Message;
        }
        else
        {
            _fieldErrors[key] = null;
        }
    }

    /// <summary>Gets the runtime options for a loader-driven field, if set.</summary>
    /// <param name="key">The field key.</param>
    /// <returns>The options, or null when none have been loaded.</returns>
    public IReadOnlyList<SelectOption>? GetFieldOptions(string key) =>
        _fieldOptions.TryGetValue(key, out IReadOnlyList<SelectOption>? options) ? options : null;

    /// <summary>Replaces the runtime options for a field.</summary>
    /// <param name="key">The field key.</param>
    /// <param name="options">The new options.</param>
    public void SetFieldOptions(string key, IReadOnlyList<SelectOption> options)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(options);

        _fieldOptions[key] = options;
        RaiseStateChanged();
    }

    /// <summary>True when the field is currently showing a loading spinner.</summary>
    /// <param name="key">The field key.</param>
    /// <returns>Whether the field is loading.</returns>
    public bool IsFieldLoading(string key) =>
        _fieldLoading.TryGetValue(key, out bool loading) && loading;

    /// <summary>Sets the loading state for a field.</summary>
    /// <param name="key">The field key.</param>
    /// <param name="loading">Whether the field is loading.</param>
    public void SetFieldLoading(string key, bool loading)
    {
        ArgumentNullException.ThrowIfNull(key);

        _fieldLoading[key] = loading;
        RaiseStateChanged();
    }

    /// <summary>
    /// True when the field is enabled. Fields are enabled unless explicitly
    /// disabled via <see cref="SetFieldEnabled"/>.
    /// </summary>
    /// <param name="key">The field key.</param>
    /// <returns>Whether the field is enabled.</returns>
    public bool IsFieldEnabled(string key) =>
        !_fieldEnabled.TryGetValue(key, out bool enabled) || enabled;

    /// <summary>Sets the enabled state for a field.</summary>
    /// <param name="key">The field key.</param>
    /// <param name="enabled">Whether the field is enabled.</param>
    public void SetFieldEnabled(string key, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(key);

        _fieldEnabled[key] = enabled;
        RaiseStateChanged();
    }

    /// <summary>Enqueues a toast and notifies the toast container.</summary>
    /// <param name="message">The toast message.</param>
    /// <param name="severity">The toast severity.</param>
    public void EnqueueToast(string message, ToastSeverity severity)
    {
        _toasts.Add(new ToastMessage { Message = message, Severity = severity });
        ToastsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Removes a toast from the queue by id.</summary>
    /// <param name="id">The toast identifier.</param>
    public void DismissToast(Guid id)
    {
        if (_toasts.RemoveAll(t => t.Id == id) > 0)
        {
            ToastsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Marks the live document as the new clean baseline, e.g. after a save.
    /// </summary>
    public void AcceptAsSaved()
    {
        _dirtyTracker.Snapshot(Document);
        _dirtyTracker.Update(Document);
        RaiseStateChanged();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _dirtyTracker.DirtyStateChanged -= OnDirtyStateChanged;
        _categoryCts.Cancel();
        _categoryCts.Dispose();
    }

    private void OnDirtyStateChanged(object? sender, EventArgs e) => RaiseStateChanged();

    private void ResetCategoryToken()
    {
        CancellationTokenSource previous = _categoryCts;
        _categoryCts = new CancellationTokenSource();
        previous.Cancel();
        previous.Dispose();
    }

    private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}
