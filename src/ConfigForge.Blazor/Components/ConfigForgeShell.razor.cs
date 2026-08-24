using ConfigForge.Abstractions;
using ConfigForge.Blazor.Services;
using ConfigForge.Core.Documents;
using ConfigForge.Core.Schema;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ConfigForge.Blazor.Components;

/// <summary>
/// The root ConfigForge component. It seeds the editing session from the supplied
/// schema and document, subscribes to session changes to re-render, and wires the
/// save, discard, and generate flows.
/// </summary>
public sealed partial class ConfigForgeShell : ComponentBase, IDisposable
{
    private bool _showGenerateDialog;
    private bool _initialized;
    private bool _disposed;
    private bool _codePanelOpen;
    private bool _copied;
    private string? _codeError;
    private CodeView _codeView = CodeView.Config;

    private bool _showAddEntryDialog;
    private int _addEntryCategoryIndex;
    private string? _addEntryVariant;
    private string _addEntryName = string.Empty;

    // Non-null while the add dialog edits a provisional entry in place (control mode): the entry is
    // staged in the document on open so its label field renders with its real control (dropdown,
    // loader, enum), and is committed on confirm or reverted on cancel.
    private string? _addEntryKey;

    // The collection field's value before staging, restored verbatim if the dialog is cancelled.
    private object? _addEntryOriginalCollection;
    private CollectionEntryRef? _removeEntryRef;
    private bool _showDiscardConfirm;

    private enum CodeView
    {
        Config,
        Schema,
    }

    /// <summary>The schema to edit against.</summary>
    [Parameter]
    [EditorRequired]
    public ConfigSchema Schema { get; set; } = new();

    /// <summary>The document to edit. A clone is taken so the original is untouched.</summary>
    [Parameter]
    public ConfigDocument? Document { get; set; }

    /// <summary>The originating parse result, surfaced in the banners and summary.</summary>
    [Parameter]
    public ConfigDocumentParseResult? ParseResult { get; set; }

    /// <summary>
    /// The raw document JSON. Retained so the malformed-JSON fallback editor can
    /// show the original text for correction when <see cref="ParseResult"/> reports a
    /// <c>JsonError</c>.
    /// </summary>
    [Parameter]
    public string? RawDocumentJson { get; set; }

    /// <summary>The host mode; generation controls are shown only in Open mode.</summary>
    [Parameter]
    public ConfigForgeMode Mode { get; set; } = ConfigForgeMode.Open;

    /// <summary>Whether the header's generate-document button is shown. Default true.</summary>
    [Parameter]
    public bool ShowGenerateButton { get; set; } = true;

    /// <summary>Custom host-supplied links rendered in the header action area.</summary>
    [Parameter]
    public IReadOnlyList<ConfigForgeHeaderAction> HeaderActions { get; set; } = [];

    /// <summary>
    /// Whether the collapsible code panel (live Config JSON, and the Schema when
    /// <see cref="SchemaJson"/> is supplied) and its header toggle are available.
    /// Default true.
    /// </summary>
    [Parameter]
    public bool ShowCodePanel { get; set; } = true;

    /// <summary>
    /// The raw schema JSON, shown on the code panel's Schema tab. When null the Schema
    /// tab is hidden and only the live Config JSON is shown.
    /// </summary>
    [Parameter]
    public string? SchemaJson { get; set; }

    /// <summary>
    /// The label of the category to activate. Lets a host deep-link to a category
    /// (e.g. from the URL). Matched case-insensitively against the schema categories.
    /// </summary>
    [Parameter]
    public string? ActiveCategoryLabel { get; set; }

    /// <summary>Raised with the new category label when the active category changes.</summary>
    [Parameter]
    public EventCallback<string> OnCategoryChanged { get; set; }

    /// <summary>
    /// The key of the entry to select within the active collection category. Lets a host deep-link
    /// to a single entry (e.g. from the URL). Ignored for non-collection categories or an unknown
    /// key.
    /// </summary>
    [Parameter]
    public string? ActiveEntryKey { get; set; }

    /// <summary>
    /// Raised with the selected entry key (or null when none) when the selection within a
    /// collection category changes, so a host can reflect it in the URL.
    /// </summary>
    [Parameter]
    public EventCallback<string?> OnEntryChanged { get; set; }

    /// <summary>Raised when the user saves; receives the current document.</summary>
    [Parameter]
    public EventCallback<ConfigDocument> OnSave { get; set; }

    [Inject]
    private EditingSession Session { get; set; } = default!;

    [Inject]
    private IThemeProvider ThemeProvider { get; set; } = default!;

    [Inject]
    private IConfigDocumentGenerator Generator { get; set; } = default!;

    [Inject]
    private IConfigDocumentEngine Engine { get; set; } = default!;

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    [Inject]
    private IJsonFormsRuleEvaluator RuleEvaluator { get; set; } = default!;

    private ThemeDefinition Theme => ThemeProvider.GetTheme();

    private bool HasSchemaJson => !string.IsNullOrEmpty(SchemaJson);

    // The Config tab is editable and shows the full live document; the Schema tab is
    // read-only. Editing the Config JSON parses it straight back into the form.
    private string CurrentCode =>
        _codeView == CodeView.Schema && HasSchemaJson
            ? SchemaJson!
            : Engine.Serialize(Session.Document);

    private IReadOnlyList<CategoryElement> Categories => Schema.Categories;

    private string HeaderTitle => string.IsNullOrEmpty(Schema.Name) ? "ConfigForge" : Schema.Name;

    private string? HeaderSubtitle =>
        string.IsNullOrEmpty(Schema.Version) ? null : $"v{Schema.Version}";

    private bool IsValid => !Session.HasVisibleErrors;

    /// <inheritdoc />
    protected override void OnInitialized() => Session.StateChanged += OnSessionChanged;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (!_initialized)
        {
            _initialized = true;
            ConfigDocument document = Document?.Clone() ?? new ConfigDocument();
            Session.Initialize(Schema, document, ParseResult, RawDocumentJson);
        }

        SyncActiveCategoryFromLabel();
        SyncActiveEntryFromKey();
        EnsureActiveCategoryUsable();
    }

    private void SyncActiveCategoryFromLabel()
    {
        if (string.IsNullOrEmpty(ActiveCategoryLabel))
        {
            return;
        }

        IReadOnlyList<CategoryElement> categories = Schema.Categories;
        for (int i = 0; i < categories.Count; i++)
        {
            if (
                string.Equals(
                    categories[i].Label,
                    ActiveCategoryLabel,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                Session.SetActiveCategory(i);
                break;
            }
        }
    }

    // Apply a host-supplied entry deep-link. Sets the selection directly (no OnEntryChanged) so it
    // does not echo back to the host and loop, mirroring SyncActiveCategoryFromLabel.
    private void SyncActiveEntryFromKey()
    {
        if (ActiveEntryKey is not { Length: > 0 } entryKey)
        {
            return;
        }

        int active = Session.ActiveCategoryIndex;
        IReadOnlyList<CategoryElement> categories = Schema.Categories;
        if (
            active >= 0
            && active < categories.Count
            && categories[active].CollectionKey is { Length: > 0 } collectionKey
            && Session.Document[collectionKey] is IDictionary<string, object?> map
            && map.ContainsKey(entryKey)
        )
        {
            Session.SetSelectedEntry(collectionKey, entryKey);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            Session.StateChanged -= OnSessionChanged;
        }
    }

    private void OnSessionChanged(object? sender, EventArgs e)
    {
        // A field change may have locked (or hidden) the active tab; move off it before rendering.
        EnsureActiveCategoryUsable();
        InvokeAsync(StateHasChanged);
    }

    private async Task OnSelectCategory(int index)
    {
        // Ignore selection of a locked/hidden category (a disabled button won't fire this, but the
        // deep-link path can still reach here).
        IReadOnlyList<CategoryElement> categories = Schema.Categories;
        if (index >= 0 && index < categories.Count)
        {
            (bool visible, bool enabled) = CategoryVisibility.Resolve(
                categories[index],
                RuleEvaluator,
                Session.Document
            );
            if (!visible || !enabled)
            {
                return;
            }
        }

        Session.SetActiveCategory(index);

        // A collection category is only a container for its entries; its own page is just the
        // "select an entry" placeholder. Landing there is a dead end, so select the first entry
        // (the add affordance stays in the sidebar) unless one is already selected.
        if (
            index >= 0
            && index < categories.Count
            && categories[index].CollectionKey is { Length: > 0 } collectionKey
            && string.IsNullOrEmpty(Session.GetSelectedEntry(collectionKey))
            && FirstEntryKey(collectionKey) is { } firstEntry
        )
        {
            await SetSelectedEntryAsync(collectionKey, firstEntry);
        }

        if (OnCategoryChanged.HasDelegate && index >= 0 && index < categories.Count)
        {
            await OnCategoryChanged.InvokeAsync(categories[index].Label);
        }
    }

    // The single place a collection selection changes by user action: sets it and notifies the host
    // (so it can reflect the entry in the URL). The deep-link apply path bypasses this to avoid a loop.
    private async Task SetSelectedEntryAsync(string collectionKey, string? entryKey)
    {
        Session.SetSelectedEntry(collectionKey, entryKey);
        if (OnEntryChanged.HasDelegate)
        {
            await OnEntryChanged.InvokeAsync(entryKey);
        }
    }

    private string? FirstEntryKey(string collectionKey) =>
        Session.Document[collectionKey] is IDictionary<string, object?> { Count: > 0 } map
            ? map.Keys.First()
            : null;

    // If the active category is locked or hidden by a rule, switch to the first usable one so the
    // canvas never shows a tab the sidebar disables.
    private void EnsureActiveCategoryUsable()
    {
        IReadOnlyList<CategoryElement> categories = Schema.Categories;
        int active = Session.ActiveCategoryIndex;
        if (active < 0 || active >= categories.Count)
        {
            return;
        }

        (bool activeVisible, bool activeEnabled) = CategoryVisibility.Resolve(
            categories[active],
            RuleEvaluator,
            Session.Document
        );
        if (activeVisible && activeEnabled)
        {
            return;
        }

        for (int i = 0; i < categories.Count; i++)
        {
            (bool visible, bool enabled) = CategoryVisibility.Resolve(
                categories[i],
                RuleEvaluator,
                Session.Document
            );
            if (visible && enabled)
            {
                Session.SetActiveCategory(i);
                return;
            }
        }
    }

    // ----- Collection master/detail (categories backed by a map field) -----

    private CategoryElement? AddCategory =>
        _addEntryCategoryIndex >= 0 && _addEntryCategoryIndex < Schema.Categories.Count
            ? Schema.Categories[_addEntryCategoryIndex]
            : null;

    private FieldDefinition? AddValueField =>
        AddCategory?.CollectionKey is { Length: > 0 } key
        && Schema.Fields.TryGetValue(key, out FieldDefinition? mapField)
            ? mapField.ValueField
            : null;

    private IReadOnlyList<OneOfVariant> AddVariants => AddValueField?.OneOfVariants ?? [];

    private bool AddIsOneOf => AddValueField?.DiscriminatorKey is { Length: > 0 };

    private async Task OnSelectCollectionEntry(CollectionEntryRef entry)
    {
        await OnSelectCategory(entry.CategoryIndex);
        if (Schema.Categories[entry.CategoryIndex].CollectionKey is { Length: > 0 } key)
        {
            await SetSelectedEntryAsync(key, entry.EntryKey);
        }
    }

    private void OnAddCollectionEntry(int categoryIndex)
    {
        _addEntryCategoryIndex = categoryIndex;
        _addEntryName = string.Empty;
        _addEntryVariant = AddVariants.Count > 0 ? AddVariants[0].DiscriminatorValue : null;
        _addEntryKey = null;

        // When the label maps to a real entry field, edit the entry in place: stage it now so the
        // label field renders with its own control (and any loader resolves against a live path).
        if (
            AddCategory?.CollectionKey is { Length: > 0 } collectionKey
            && ResolveAddLabelTemplate() is not null
        )
        {
            _addEntryOriginalCollection = Session.GetFieldValue(collectionKey);
            _addEntryKey = Session.StageMapEntry(
                collectionKey,
                IsKeyless(collectionKey),
                NewEntrySeed()
            );
        }

        _showAddEntryDialog = true;
    }

    private void CancelAddEntry()
    {
        if (
            _addEntryKey is { } stagedKey
            && AddCategory?.CollectionKey is { Length: > 0 } collectionKey
        )
        {
            Session.DiscardStagedEntry(collectionKey, stagedKey, _addEntryOriginalCollection);
        }

        CleanupAddDialog();
    }

    private async Task ConfirmAddEntry()
    {
        CategoryElement? category = AddCategory;
        if (category?.CollectionKey is not { Length: > 0 } collectionKey)
        {
            CleanupAddDialog();
            return;
        }

        // Control mode: the entry is already staged; make it permanent and select it.
        if (_addEntryKey is { } stagedKey)
        {
            Session.CommitStagedEntry(collectionKey);
            Session.SetActiveCategory(_addEntryCategoryIndex);
            await SetSelectedEntryAsync(collectionKey, stagedKey);
            CleanupAddDialog();
            return;
        }

        // Name mode: no label control resolved, so the free-text name seeds the flat label field.
        Dictionary<string, object?> value = NewEntrySeed();
        if (
            !string.IsNullOrWhiteSpace(_addEntryName)
            && category.CollectionEntryLabelKey is { Length: > 0 } labelKey
            && !labelKey.Contains('/', StringComparison.Ordinal)
        )
        {
            value[labelKey] = _addEntryName.Trim();
        }

        string entryKey = Session.AddMapEntry(collectionKey, IsKeyless(collectionKey), value);
        Session.SetActiveCategory(_addEntryCategoryIndex);
        await SetSelectedEntryAsync(collectionKey, entryKey);
        CleanupAddDialog();
    }

    private void CleanupAddDialog()
    {
        _showAddEntryDialog = false;
        _addEntryKey = null;
        _addEntryOriginalCollection = null;
    }

    // KeyFormat lives on the map field itself, not on the entry-value template. Reading it off the
    // value template yields null, so uuid-keyed collections would wrongly get sequential "keyN"
    // keys instead of a GUID.
    private bool IsKeyless(string collectionKey) =>
        Schema.Fields.TryGetValue(collectionKey, out FieldDefinition? mapField)
        && string.Equals(mapField.KeyFormat, "uuid", StringComparison.Ordinal);

    private Dictionary<string, object?> NewEntrySeed()
    {
        Dictionary<string, object?> seed = new(StringComparer.Ordinal);
        if (
            AddValueField?.DiscriminatorKey is { Length: > 0 } disc
            && _addEntryVariant is { Length: > 0 }
        )
        {
            seed[disc] = _addEntryVariant;
        }

        return seed;
    }

    // Switching the type in control mode resets the staged entry to the new variant so the label
    // field (which may differ per variant) resolves against a clean shape.
    private void OnAddVariantChanged(string? variant)
    {
        _addEntryVariant = variant;
        if (
            _addEntryKey is { } stagedKey
            && AddIsOneOf
            && AddCategory?.CollectionKey is { Length: > 0 } collectionKey
        )
        {
            Session.SetStagedEntryValue(collectionKey, stagedKey, NewEntrySeed());
        }
    }

    // The label field to render in the add dialog, rebased onto the staged entry's path, or null
    // when the label has no dedicated control (name mode: a free-text name is used instead).
    private FieldDefinition? AddLabelField
    {
        get
        {
            if (
                _addEntryKey is not { } stagedKey
                || AddCategory?.CollectionKey is not { Length: > 0 } collectionKey
                || ResolveAddLabelTemplate() is not { } template
            )
            {
                return null;
            }

            return template.WithKey($"{collectionKey}/{stagedKey}/{template.Key}");
        }
    }

    // The entry field the collection's label points at (a direct child of the value template, or of
    // the selected oneof variant). Null when the label key is unset, nested, or not a real field.
    private FieldDefinition? ResolveAddLabelTemplate()
    {
        if (
            AddCategory?.CollectionEntryLabelKey is not { Length: > 0 } labelKey
            || labelKey.Contains('/', StringComparison.Ordinal)
            || AddValueField is not { } value
        )
        {
            return null;
        }

        if (value.DiscriminatorKey is { Length: > 0 })
        {
            OneOfVariant? variant =
                value.OneOfVariants.FirstOrDefault(v =>
                    string.Equals(v.DiscriminatorValue, _addEntryVariant, StringComparison.Ordinal)
                ) ?? (value.OneOfVariants.Count > 0 ? value.OneOfVariants[0] : null);
            return variant?.Children.FirstOrDefault(c =>
                string.Equals(c.Key, labelKey, StringComparison.Ordinal)
            );
        }

        return value.Children.FirstOrDefault(c =>
            string.Equals(c.Key, labelKey, StringComparison.Ordinal)
        );
    }

    private void OnRemoveCollectionEntry(CollectionEntryRef entry) => _removeEntryRef = entry;

    private void CancelRemoveEntry() => _removeEntryRef = null;

    private async Task ConfirmRemoveEntry()
    {
        if (
            _removeEntryRef is { } entry
            && Schema.Categories[entry.CategoryIndex].CollectionKey is { Length: > 0 } collectionKey
            && Session.RemoveMapEntry(collectionKey, entry.EntryKey)
            && string.Equals(
                Session.GetSelectedEntry(collectionKey),
                entry.EntryKey,
                StringComparison.Ordinal
            )
        )
        {
            await SetSelectedEntryAsync(collectionKey, null);
        }

        _removeEntryRef = null;
    }

    private void ToggleCodePanel()
    {
        _codePanelOpen = !_codePanelOpen;
        _copied = false;
    }

    private void SetCodeView(CodeView view)
    {
        _codeView = view;
        _copied = false;
        _codeError = null;
    }

    /// <summary>
    /// Parses edited Config JSON straight back into the live document, so an existing
    /// config can be pasted into the panel and drive the form. Invalid JSON shows an
    /// inline error and leaves the current form untouched.
    /// </summary>
    private Task OnConfigJsonChangedAsync(ChangeEventArgs args)
    {
        string json = args.Value as string ?? string.Empty;
        ConfigDocumentParseResult result = Engine.Parse(json, Schema);

        if (result.JsonError is not null)
        {
            _codeError = result.JsonError;
            return Task.CompletedTask;
        }

        _codeError = null;
        Session.ReplaceDocument(result.Document, result);
        return Task.CompletedTask;
    }

    private async Task CopyCodeAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("navigator.clipboard.writeText", CurrentCode);
            _copied = true;
        }
        catch (JSException)
        {
            // Clipboard access can be denied; leave the button label unchanged.
        }
    }

    private void ShowGenerateDialog() => _showGenerateDialog = true;

    private void HideGenerateDialog() => _showGenerateDialog = false;

    private async Task SaveAsync()
    {
        Session.MarkSaveAttempted();
        Session.Revalidate();
        if (Session.ParseResult is { IsValid: false } invalid)
        {
            NavigateToFirstProblem(invalid);
            Session.EnqueueToast(
                "Please fix the highlighted required fields before saving.",
                ToastSeverity.Warning
            );
            return;
        }

        try
        {
            await OnSave.InvokeAsync(Session.Document);
            Session.AcceptAsSaved();
            Session.EnqueueToast("Configuration saved.", ToastSeverity.Success);
        }
#pragma warning disable CA1031 // The host save handler is untrusted; surface any failure instead of tearing down the circuit.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Session.EnqueueToast($"Save failed: {ex.Message}", ToastSeverity.Danger);
        }
    }

    private static string? FirstProblemKey(ConfigDocumentParseResult result)
    {
        if (result.MissingRequiredKeys.Count > 0)
        {
            return result.MissingRequiredKeys[0];
        }

        return result.InvalidValues.Count > 0 ? result.InvalidValues[0].Key : null;
    }

    // Jump the sidebar to the entry that owns the first validation problem, so a required field
    // buried in a collection entry (e.g. connectors/<guid>/…) is reachable rather than hidden.
    private void NavigateToFirstProblem(ConfigDocumentParseResult result)
    {
        string? key = FirstProblemKey(result);
        if (key is null)
        {
            return;
        }

        string[] segments = key.Split('/');
        IReadOnlyList<CategoryElement> categories = Schema.Categories;
        for (int i = 0; i < categories.Count; i++)
        {
            if (
                categories[i].CollectionKey is { Length: > 0 } collectionKey
                && string.Equals(collectionKey, segments[0], StringComparison.Ordinal)
                && segments.Length > 1
            )
            {
                Session.SetActiveCategory(i);
                Session.SetSelectedEntry(collectionKey, segments[1]);
                return;
            }
        }
    }

    private void RequestDiscard() => _showDiscardConfirm = true;

    private void CancelDiscard() => _showDiscardConfirm = false;

    private void ConfirmDiscard()
    {
        _showDiscardConfirm = false;
        Session.Discard();
    }

    private Task OnGenerateConfirmedAsync(GenerateDocumentDialog.GenerationMode mode)
    {
        ConfigDocument generated =
            mode == GenerateDocumentDialog.GenerationMode.Example
                ? Generator.GenerateExample(Schema)
                : Generator.GenerateEmpty(Schema);

        Session.Initialize(Schema, generated);
        _showGenerateDialog = false;
        return Task.CompletedTask;
    }
}
