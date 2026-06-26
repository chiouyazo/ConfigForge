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

    private bool IsValid => Session.ParseResult?.IsValid ?? true;

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

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            Session.StateChanged -= OnSessionChanged;
        }
    }

    private void OnSessionChanged(object? sender, EventArgs e) => InvokeAsync(StateHasChanged);

    private async Task OnSelectCategory(int index)
    {
        Session.SetActiveCategory(index);

        IReadOnlyList<CategoryElement> categories = Schema.Categories;
        if (OnCategoryChanged.HasDelegate && index >= 0 && index < categories.Count)
        {
            await OnCategoryChanged.InvokeAsync(categories[index].Label).ConfigureAwait(false);
        }
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
            await JS.InvokeVoidAsync("navigator.clipboard.writeText", CurrentCode)
                .ConfigureAwait(false);
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
        await OnSave.InvokeAsync(Session.Document).ConfigureAwait(false);
        Session.AcceptAsSaved();
    }

    private void Discard()
    {
        ConfigDocument document = Document?.Clone() ?? new ConfigDocument();
        Session.ReplaceDocument(document, ParseResult);
        Session.AcceptAsSaved();
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
