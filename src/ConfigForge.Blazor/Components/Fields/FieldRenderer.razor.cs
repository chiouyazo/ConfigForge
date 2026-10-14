using System.Collections;
using System.Globalization;
using ConfigForge.Abstractions;
using ConfigForge.Blazor.Services;
using ConfigForge.Core.Plugins;
using ConfigForge.Core.Schema;
using Microsoft.AspNetCore.Components;

namespace ConfigForge.Blazor.Components.Fields;

/// <summary>
/// Resolves a <see cref="FieldDefinition"/> to a concrete control: either a
/// registered plugin control (rendered via <c>DynamicComponent</c>) or a built-in
/// field component. Static enum options come from the schema constraints; loader
/// driven options are fetched through the <see cref="IActionDispatcher"/>.
/// </summary>
public sealed partial class FieldRenderer : ComponentBase, IDisposable
{
    private string? _loadedFor;
    private bool _disposed;
    private CancellationTokenSource? _loadCts;

    /// <summary>The resolved field definition driving the control choice.</summary>
    [Parameter]
    [EditorRequired]
    public FieldDefinition Field { get; set; } = new();

    /// <summary>True when the field is disabled by a rule or by being read-only.</summary>
    [Parameter]
    public bool Disabled { get; set; }

    [Inject]
    private EditingSession Session { get; set; } = default!;

    [Inject]
    private IActionDispatcher Dispatcher { get; set; } = default!;

    [Inject]
    private IServiceProvider Services { get; set; } = default!;

    [Inject]
    private IJsonFormsRuleEvaluator RuleEvaluator { get; set; } = default!;

    /// <summary>
    /// Resolves the field's effective visibility and disabled state from its rules, its
    /// programmatic enable state, its read-only flag, and the disabled state cascaded from a
    /// parent container. Evaluated here (not only in <c>ControlRenderer</c>) so it applies to
    /// fields rendered directly inside a map/array/oneof entry too.
    /// </summary>
    private (bool Visible, bool Disabled) ResolveState()
    {
        if (Field.Hidden)
        {
            return (false, true);
        }

        (bool visible, bool ruleEnabled) = RuleEvaluation.Resolve(
            Field.Rules,
            RuleEvaluator,
            Session.Document
        );
        bool enabled =
            ruleEnabled && !Field.ReadOnly && Session.IsFieldEnabled(Field.Key) && !Disabled;
        return (visible, !enabled);
    }

    private ConfigDocument Document => Session.Document;

    private EventCallback<FieldChangedArgs> OnFieldChanged =>
        EventCallback.Factory.Create<FieldChangedArgs>(this, OnFieldChangedAsync);

    private ControlDescriptor Descriptor =>
        new()
        {
            Key = Field.Key,
            ControlType = Field.ControlType,
            Title = Field.Title,
            Description = Field.Description,
            Tooltip = Field.Tooltip,
            Placeholder = Field.Placeholder,
            Unit = Field.Unit,
            Required = Field.Required,
            ReadOnly = Field.ReadOnly,
            Options = Field.SchemaConstraints,
        };

    private Type? PluginComponentType
    {
        get
        {
            if (
                Services.GetService(typeof(IPluginCatalog)) is IPluginCatalog catalog
                && catalog.TryGetControl(Field.ControlType, out Type? type)
            )
            {
                return type;
            }

            return null;
        }
    }

    /// <summary>
    /// The optional capability widget catalog, present only when the host has remote instances
    /// enabled. <see cref="CapabilityWidgetHost"/> re-resolves the registration for
    /// <see cref="FieldDefinition.ControlType"/> from this catalog on every one of its own
    /// refreshes rather than once here, since the backing capability assembly can be reloaded
    /// (redeployed) at any time.
    /// </summary>
    private ICapabilityWidgetCatalog? CapabilityWidgetCatalog =>
        Services.GetService(typeof(ICapabilityWidgetCatalog)) as ICapabilityWidgetCatalog;

    /// <summary>
    /// True when <see cref="Field"/>'s control type names a capability widget a remote instance's
    /// own capability assembly registered (never a local <see cref="IPluginCatalog"/> control).
    /// </summary>
    private bool IsCapabilityWidget =>
        CapabilityWidgetCatalog?.TryGetWidget(Field.ControlType, out _) ?? false;

    private Dictionary<string, object> PluginParameters =>
        new(StringComparer.Ordinal)
        {
            [nameof(IConfigControl.Control)] = Descriptor,
            [nameof(IConfigControl.Document)] = Document,
            [nameof(IConfigControl.OnFieldChanged)] = OnFieldChanged,
        };

    private IReadOnlyList<SelectOption> Options =>
        Session.GetFieldOptions(Field.Key) ?? StaticOptions();

    private bool Loading => Session.IsFieldLoading(Field.Key);

    private bool LoadFailed => Session.IsFieldLoadFailed(Field.Key);

    private string? FieldError => Session.GetFieldError(Field.Key);

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        // Keyed by field as well as loader: a collection shows one entry at a time in the same
        // component, so the field key changes under a reused instance and its options must load too.
        if (
            Field.LoaderId is { Length: > 0 } loaderId
            && !string.Equals(_loadedFor, $"{Field.Key}|{loaderId}", StringComparison.Ordinal)
            && Session.GetFieldOptions(Field.Key) is null
        )
        {
            _loadedFor = $"{Field.Key}|{loaderId}";
            await LoadOptionsAsync(loaderId);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _loadCts?.Cancel();
            _loadCts?.Dispose();
        }
    }

    private Task RetryLoadAsync() =>
        Field.LoaderId is { Length: > 0 } loaderId
            ? LoadOptionsAsync(loaderId)
            : Task.CompletedTask;

    private async Task LoadOptionsAsync(string loaderId)
    {
        if (_loadCts is not null)
        {
            await _loadCts.CancelAsync();
            _loadCts.Dispose();
        }

        CancellationTokenSource cts = new();
        _loadCts = cts;

        string key = Field.Key;
        Session.SetFieldLoadFailed(key, false);
        Session.SetFieldLoading(key, true);
        try
        {
            ActionContext context = new(Session, Services, key);
            IReadOnlyList<SelectOption> options = await Dispatcher
                .DispatchLoaderAsync(loaderId, context)
                .WaitAsync(Session.OptionLoadTimeout, cts.Token);
            Session.SetFieldOptions(key, options);
        }
        catch (TimeoutException)
        {
            Session.SetFieldLoadFailed(key, true);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer load or the field left the screen: the next render loads again.
            if (string.Equals(_loadedFor, $"{key}|{loaderId}", StringComparison.Ordinal))
            {
                _loadedFor = null;
            }
        }
        finally
        {
            Session.SetFieldLoading(key, false);
        }
    }

    private Task OnFieldChangedAsync(FieldChangedArgs args)
    {
        Session.SetFieldValue(args.Key, args.Value);
        return Task.CompletedTask;
    }

    private List<SelectOption> StaticOptions()
    {
        // A plain enum carries its values on the field; an array/checklist of enums
        // (e.g. a multi-select of weekdays) carries them on the items schema.
        object? raw = null;
        if (Field.SchemaConstraints.TryGetValue("enum", out object? fieldEnum))
        {
            raw = fieldEnum;
        }
        else if (
            Field.SchemaConstraints.TryGetValue("items", out object? items)
            && items is IReadOnlyDictionary<string, object?> itemMap
            && itemMap.TryGetValue("enum", out object? itemEnum)
        )
        {
            raw = itemEnum;
        }

        if (raw is not IEnumerable values || raw is string)
        {
            return [];
        }

        List<SelectOption> options = [];
        foreach (object? value in values)
        {
            string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            options.Add(new SelectOption { Value = text, Label = text });
        }

        return options;
    }
}
