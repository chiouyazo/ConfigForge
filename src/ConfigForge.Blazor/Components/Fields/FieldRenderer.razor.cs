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

    private string? FieldError => Session.GetFieldError(Field.Key);

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        if (
            Field.LoaderId is { Length: > 0 } loaderId
            && !string.Equals(_loadedFor, loaderId, StringComparison.Ordinal)
            && Session.GetFieldOptions(Field.Key) is null
        )
        {
            _loadedFor = loaderId;
            await LoadOptionsAsync(loaderId).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }

    private async Task LoadOptionsAsync(string loaderId)
    {
        Session.SetFieldLoading(Field.Key, true);
        try
        {
            ActionContext context = new(Session, Services);
            IReadOnlyList<SelectOption> options = await Dispatcher
                .DispatchLoaderAsync(loaderId, context)
                .ConfigureAwait(false);
            Session.SetFieldOptions(Field.Key, options);
        }
        finally
        {
            Session.SetFieldLoading(Field.Key, false);
        }
    }

    private Task OnFieldChangedAsync(FieldChangedArgs args)
    {
        Session.SetFieldValue(args.Key, args.Value);
        return Task.CompletedTask;
    }

    private List<SelectOption> StaticOptions()
    {
        if (
            !Field.SchemaConstraints.TryGetValue("enum", out object? raw)
            || raw is not IEnumerable values
        )
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
