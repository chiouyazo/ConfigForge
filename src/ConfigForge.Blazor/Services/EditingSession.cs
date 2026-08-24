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
    private readonly IConfigDocumentEngine _engine;
    private readonly Dictionary<string, IReadOnlyList<SelectOption>> _fieldOptions = new(
        StringComparer.Ordinal
    );
    private readonly Dictionary<string, bool> _fieldLoading = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _fieldEnabled = new(StringComparer.Ordinal);

    private readonly Dictionary<string, string?> _fieldErrors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _selectedEntries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _selectedSections = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _selectedGroupTabs = new(StringComparer.Ordinal);
    private readonly HashSet<string> _touchedKeys = new(StringComparer.Ordinal);
    private readonly List<ToastMessage> _toasts = [];

    private bool _saveAttempted;
    private ConfigDocument _discardBaseline = new();
    private CancellationTokenSource _categoryCts = new();
    private bool _disposed;

    /// <summary>Creates a session backed by the supplied dirty-state tracker.</summary>
    /// <param name="dirtyTracker">The dirty-state tracker for this session.</param>
    /// <param name="pluginCatalog">The plugin catalog used to resolve field validators.</param>
    /// <param name="engine">The document engine used to revalidate the document live on edits.</param>
    public EditingSession(
        IDirtyStateTracker dirtyTracker,
        IPluginCatalog pluginCatalog,
        IConfigDocumentEngine engine
    )
    {
        ArgumentNullException.ThrowIfNull(dirtyTracker);
        ArgumentNullException.ThrowIfNull(pluginCatalog);
        ArgumentNullException.ThrowIfNull(engine);
        _dirtyTracker = dirtyTracker;
        _pluginCatalog = pluginCatalog;
        _engine = engine;
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
        _discardBaseline = document.Clone();
        ParseResult = parseResult;
        RawJson = rawJson;
        ActiveCategoryIndex = 0;

        _fieldOptions.Clear();
        _fieldLoading.Clear();
        _fieldEnabled.Clear();
        _fieldErrors.Clear();
        _selectedEntries.Clear();
        _selectedSections.Clear();
        _selectedGroupTabs.Clear();
        _touchedKeys.Clear();
        _saveAttempted = false;

        RefreshIgnoredKeys();
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
        RefreshIgnoredKeys();
        _dirtyTracker.Update(document);
        RaiseStateChanged();
    }

    private void RefreshIgnoredKeys()
    {
        if (Schema is { } schema)
        {
            _dirtyTracker.IgnoredKeys = new HashSet<string>(
                SchemaWalker.UntrackedKeys(schema, Document),
                StringComparer.Ordinal
            );
        }
    }

    /// <summary>
    /// Recomputes the validation state against the live document, so the save button, banner, and
    /// summary reflect edits immediately (and a fixed field clears its error without a save round).
    /// </summary>
    public void Revalidate()
    {
        RecomputeValidation();
        RaiseStateChanged();
    }

    private void RecomputeValidation()
    {
        if (Schema is { } schema)
        {
            ParseResult = _engine.Validate(Document, schema);
        }
    }

    /// <summary>Records that the user attempted a save, from which point every error is shown.</summary>
    public void MarkSaveAttempted()
    {
        _saveAttempted = true;
        RaiseStateChanged();
    }

    /// <summary>
    /// Whether a validation error for a field should be shown yet. Validation runs live, but a
    /// freshly created entry has not been touched, so its errors stay hidden until the user edits
    /// the field or presses save. Avoids flagging a new entry red before the user did anything.
    /// </summary>
    public bool ShouldShowError(string key) => _saveAttempted || _touchedKeys.Contains(key);

    /// <summary>True when any currently-showable validation error exists (drives the save bar).</summary>
    public bool HasVisibleErrors =>
        ParseResult is { } result
        && (
            result.JsonError is not null
            || result.MissingRequiredKeys.Any(ShouldShowError)
            || result.InvalidValues.Any(e => ShouldShowError(e.Key))
        );

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

    /// <summary>
    /// Returns the entry key currently selected within a collection category's map
    /// (see <see cref="CategoryElement.CollectionKey"/>), or null when none is selected.
    /// </summary>
    /// <param name="collectionKey">The map field key backing the collection category.</param>
    /// <returns>The selected entry key, or null.</returns>
    public string? GetSelectedEntry(string collectionKey) =>
        _selectedEntries.TryGetValue(collectionKey, out string? entry) ? entry : null;

    /// <summary>Selects an entry within a collection category and notifies subscribers.</summary>
    /// <param name="collectionKey">The map field key backing the collection category.</param>
    /// <param name="entryKey">The entry key to select, or null to clear the selection.</param>
    public void SetSelectedEntry(string collectionKey, string? entryKey)
    {
        ArgumentNullException.ThrowIfNull(collectionKey);
        if (entryKey is null)
        {
            _selectedEntries.Remove(collectionKey);
        }
        else
        {
            _selectedEntries[collectionKey] = entryKey;
        }

        RaiseStateChanged();
    }

    /// <summary>Returns the active section (sub-tab) of a oneof field, or null when unset.</summary>
    /// <param name="fieldKey">The oneof field key (e.g. a rebased entry path).</param>
    public string? GetSelectedSection(string fieldKey) =>
        _selectedSections.TryGetValue(fieldKey, out string? section) ? section : null;

    /// <summary>Records the active section (sub-tab) of a oneof field and notifies subscribers.</summary>
    /// <param name="fieldKey">The oneof field key.</param>
    /// <param name="section">The active section name.</param>
    public void SetSelectedSection(string fieldKey, string section)
    {
        ArgumentNullException.ThrowIfNull(fieldKey);
        ArgumentNullException.ThrowIfNull(section);
        _selectedSections[fieldKey] = section;
        RaiseStateChanged();
    }

    /// <summary>Returns the active sub-tab of a grouped category, or null when none is recorded.</summary>
    /// <param name="groupKey">The group (top-level category) label.</param>
    public string? GetSelectedGroupTab(string groupKey) =>
        _selectedGroupTabs.TryGetValue(groupKey, out string? tab) ? tab : null;

    /// <summary>
    /// Records the active sub-tab of a grouped category, so section-scoped actions can match the
    /// tab that is showing. No-ops (no notification) when unchanged, so a component can persist its
    /// default tab on every render without a re-render loop.
    /// </summary>
    /// <param name="groupKey">The group (top-level category) label.</param>
    /// <param name="tabLabel">The active sub-tab label.</param>
    public void SetSelectedGroupTab(string groupKey, string tabLabel)
    {
        ArgumentNullException.ThrowIfNull(groupKey);
        ArgumentNullException.ThrowIfNull(tabLabel);
        if (
            _selectedGroupTabs.TryGetValue(groupKey, out string? existing)
            && string.Equals(existing, tabLabel, StringComparison.Ordinal)
        )
        {
            return;
        }

        _selectedGroupTabs[groupKey] = tabLabel;
        RaiseStateChanged();
    }

    /// <summary>
    /// Adds an entry to a map field and returns its key. The single place map entries are
    /// created, so the map control and the collection sidebar stay consistent (keyless maps
    /// get a generated GUID; keyed maps get a unique <c>keyN</c>).
    /// </summary>
    /// <param name="fieldKey">The map field key.</param>
    /// <param name="keyless">True for a uuid-keyed map (generate a GUID the user never sees).</param>
    /// <param name="value">The entry value (an object dictionary, or null for a scalar value).</param>
    /// <returns>The key of the added entry.</returns>
    public string AddMapEntry(string fieldKey, bool keyless, object? value)
    {
        ArgumentNullException.ThrowIfNull(fieldKey);
        Dictionary<string, object?> map = ReadMapCopy(fieldKey);
        string entryKey = keyless ? Guid.NewGuid().ToString() : UniqueMapKey(map);
        map[entryKey] = value;
        SetFieldValue(fieldKey, map);
        return entryKey;
    }

    /// <summary>Removes an entry from a map field. Returns true when an entry was removed.</summary>
    /// <param name="fieldKey">The map field key.</param>
    /// <param name="entryKey">The entry key to remove.</param>
    /// <returns>True when the entry existed and was removed.</returns>
    public bool RemoveMapEntry(string fieldKey, string entryKey)
    {
        ArgumentNullException.ThrowIfNull(fieldKey);
        Dictionary<string, object?> map = ReadMapCopy(fieldKey);
        if (!map.Remove(entryKey))
        {
            return false;
        }

        SetFieldValue(fieldKey, map);
        return true;
    }

    /// <summary>
    /// Adds a <em>provisional</em> map entry and returns its key, without marking the document
    /// dirty, validating, or touching keys. Used by the add-entry dialog so the entry's fields
    /// (loaders included) can render against a real document path while the dialog is open; the
    /// entry is made permanent with <see cref="CommitStagedEntry"/> or reverted with
    /// <see cref="DiscardStagedEntry"/>.
    /// </summary>
    /// <param name="fieldKey">The map field key.</param>
    /// <param name="keyless">True for a uuid-keyed map (generate a GUID the user never sees).</param>
    /// <param name="value">The seed entry value (e.g. an object carrying just the discriminator).</param>
    /// <returns>The key of the staged entry.</returns>
    public string StageMapEntry(string fieldKey, bool keyless, object? value)
    {
        ArgumentNullException.ThrowIfNull(fieldKey);
        Dictionary<string, object?> map = ReadMapCopy(fieldKey);
        string entryKey = keyless ? Guid.NewGuid().ToString() : UniqueMapKey(map);
        map[entryKey] = value;
        Document[fieldKey] = map;
        RaiseStateChanged();
        return entryKey;
    }

    /// <summary>Replaces a staged entry's value provisionally (e.g. when its type is switched).</summary>
    /// <param name="fieldKey">The map field key.</param>
    /// <param name="entryKey">The staged entry key.</param>
    /// <param name="value">The new entry value.</param>
    public void SetStagedEntryValue(string fieldKey, string entryKey, object? value)
    {
        ArgumentNullException.ThrowIfNull(fieldKey);
        ArgumentNullException.ThrowIfNull(entryKey);
        Dictionary<string, object?> map = ReadMapCopy(fieldKey);
        map[entryKey] = value;
        Document[fieldKey] = map;
        RaiseStateChanged();
    }

    /// <summary>
    /// Reverts a staged entry by restoring the map field to <paramref name="originalValue"/> (the
    /// value it held before staging, or null when the key was absent) and clearing any
    /// touched/validation state left by editing the entry, so cancelling the dialog leaves the
    /// document exactly as it was, not dirty.
    /// </summary>
    /// <param name="fieldKey">The map field key.</param>
    /// <param name="entryKey">The staged entry key.</param>
    /// <param name="originalValue">The map field's value before staging (null when it was absent).</param>
    public void DiscardStagedEntry(string fieldKey, string entryKey, object? originalValue)
    {
        ArgumentNullException.ThrowIfNull(fieldKey);
        ArgumentNullException.ThrowIfNull(entryKey);
        if (originalValue is null)
        {
            Document.Remove(fieldKey);
        }
        else
        {
            Document[fieldKey] = originalValue;
        }

        string prefix = fieldKey + "/" + entryKey;
        _touchedKeys.RemoveWhere(k => k.StartsWith(prefix, StringComparison.Ordinal));
        _dirtyTracker.Update(Document);
        RecomputeValidation();
        RaiseStateChanged();
    }

    /// <summary>
    /// Promotes a staged entry to a tracked change: marks the map field dirty and revalidates, so
    /// the confirmed entry participates in save/validation like any edit.
    /// </summary>
    /// <param name="fieldKey">The map field key.</param>
    public void CommitStagedEntry(string fieldKey)
    {
        ArgumentNullException.ThrowIfNull(fieldKey);
        _touchedKeys.Add(fieldKey);
        _dirtyTracker.Update(Document);
        RecomputeValidation();
        RaiseStateChanged();
    }

    private Dictionary<string, object?> ReadMapCopy(string fieldKey) =>
        Document[fieldKey] is IDictionary<string, object?> map
            ? new Dictionary<string, object?>(map, StringComparer.Ordinal)
            : new Dictionary<string, object?>(StringComparer.Ordinal);

    private static string UniqueMapKey(Dictionary<string, object?> map)
    {
        int index = map.Count + 1;
        string candidate = $"key{index}";
        while (map.ContainsKey(candidate))
        {
            index++;
            candidate = $"key{index}";
        }

        return candidate;
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
        _touchedKeys.Add(key);
        RunFieldValidator(key, value);
        RefreshIgnoredKeys();
        _dirtyTracker.Update(Document);
        RecomputeValidation();
        RaiseStateChanged();
    }

    /// <summary>
    /// Removes a field entirely (so "off" is absent, not a stored null), recomputes dirty state,
    /// and notifies subscribers. Used by the nullable-object toggle.
    /// </summary>
    /// <param name="key">The field key.</param>
    public void RemoveFieldValue(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        Document.Remove(key);
        _touchedKeys.Add(key);
        RunFieldValidator(key, null);
        RefreshIgnoredKeys();
        _dirtyTracker.Update(Document);
        RecomputeValidation();
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

        if (ParseResult is { } result && ShouldShowError(key))
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
        _discardBaseline = Document.Clone();
        _touchedKeys.Clear();
        _saveAttempted = false;
        RefreshIgnoredKeys();
        _dirtyTracker.Snapshot(Document);
        _dirtyTracker.Update(Document);
        RaiseStateChanged();
    }

    /// <summary>
    /// Reverts to the last clean baseline: the state at load, or after the most recent successful
    /// save. Discarding must not time-travel past a save (the parameter passed at page load is a
    /// stale snapshot), so the baseline is owned here, not read back from the host.
    /// </summary>
    public void Discard()
    {
        Document = _discardBaseline.Clone();
        _fieldErrors.Clear();
        _touchedKeys.Clear();
        _saveAttempted = false;
        RefreshIgnoredKeys();
        _dirtyTracker.Snapshot(Document);
        _dirtyTracker.Update(Document);
        RecomputeValidation();
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
