using ConfigForge.Abstractions;
using ConfigForge.Core.Plugins;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConfigForge.Blazor.Components.Fields;

/// <summary>
/// Renders a remote instance's capability widget in genuine per-render DI isolation: a fresh
/// <see cref="HtmlRenderer"/> is built for every refresh, with its own <see cref="IServiceProvider"/>
/// that resolves the widget's single <c>[Inject]</c> capability (if it declares one - see
/// <see cref="CapabilityWidgetRegistration.CapabilityInterfaceType"/>) to a proxy scoped to
/// exactly this instance (<see cref="CapabilityWidgetRegistration.HttpClient"/>/<see cref="CapabilityWidgetRegistration.Loader"/>),
/// falling back to the hosting circuit's own services for anything else. Nothing about one
/// widget's render touches any other widget's, whether from the same instance or a different one
/// rendering concurrently: there is no shared mutable state between renders, deliberately, unlike
/// an ambient "current instance" field a DI-registered service would read.
/// </summary>
/// <remarks>
/// The rendered markup is a snapshot, refreshed on a timer rather than kept interactive: an
/// <see cref="HtmlRenderer"/> renders to a detached component tree with no SignalR circuit of its
/// own, so the widget's own event handlers (if any) would not reach back to the browser. This
/// matches the read-only monitoring widgets this mechanism exists for (a log viewer, a job list);
/// see <c>docs/capability-loading.md</c>.
/// </remarks>
public sealed partial class CapabilityWidgetHost : ComponentBase, IAsyncDisposable
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(3);

    private CancellationTokenSource? _refreshLoopCts;
    private Task? _refreshLoop;
    private string? _html;
    private string? _error;

    /// <summary>
    /// The catalog to re-resolve the widget registration from on every refresh, rather than a
    /// registration frozen at first render: the backing capability assembly can be reloaded (a
    /// redeploy) at any time per <c>docs/capability-loading.md</c>, and a stale
    /// <see cref="CapabilityWidgetRegistration.CapabilityInterfaceType"/> from before such a
    /// reload no longer matches its own <see cref="CapabilityWidgetRegistration.Loader"/>'s
    /// current state.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public ICapabilityWidgetCatalog Catalog { get; set; } = null!;

    /// <summary>The composite control type to resolve a registration for on every refresh.</summary>
    [Parameter]
    [EditorRequired]
    public string ControlType { get; set; } = string.Empty;

    /// <summary>The control descriptor passed to the widget as <see cref="IConfigControl.Control"/>.</summary>
    [Parameter]
    [EditorRequired]
    public ControlDescriptor Control { get; set; } = new();

    /// <summary>The document passed to the widget as <see cref="IConfigControl.Document"/>.</summary>
    [Parameter]
    [EditorRequired]
    public ConfigDocument Document { get; set; } = new();

    /// <summary>Forwarded to the widget as <see cref="IConfigControl.OnFieldChanged"/>.</summary>
    [Parameter]
    public EventCallback<FieldChangedArgs> OnFieldChanged { get; set; }

    [Inject]
    private IServiceProvider CircuitServices { get; set; } = default!;

    [Inject]
    private ILoggerFactory LoggerFactory { get; set; } = default!;

    /// <inheritdoc />
    protected override void OnInitialized() => StartRefreshLoop();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_refreshLoopCts is not null)
        {
            await _refreshLoopCts.CancelAsync();
        }

        if (_refreshLoop is not null)
        {
            try
            {
                await _refreshLoop;
            }
            catch (OperationCanceledException)
            {
                // Expected: the loop observes the same cancellation it was told to stop with.
            }
        }

        _refreshLoopCts?.Dispose();
    }

    private void StartRefreshLoop()
    {
        _refreshLoopCts = new CancellationTokenSource();
        _refreshLoop = RunRefreshLoopAsync(_refreshLoopCts.Token);
    }

    private async Task RunRefreshLoopAsync(CancellationToken cancellationToken)
    {
        using PeriodicTimer timer = new(RefreshInterval);
        await RefreshAsync(cancellationToken);

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await RefreshAsync(cancellationToken);
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            string html = await RenderWidgetAsync(cancellationToken);
            _html = html;
            _error = null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // A remote widget's own render is untrusted; the host must not crash on any failure it raises.
        catch (Exception ex)
        {
            _error = $"Widget failed to render: {ex.Message}";
        }
#pragma warning restore CA1031

        await InvokeAsync(StateHasChanged);
    }

    private async Task<string> RenderWidgetAsync(CancellationToken cancellationToken)
    {
        if (!Catalog.TryGetWidget(ControlType, out CapabilityWidgetRegistration? registration))
        {
            throw new InvalidOperationException(
                $"'{ControlType}' is no longer a registered capability widget; the originating "
                    + "instance may have disconnected or redeployed its capability assembly."
            );
        }

        ServiceCollection perRenderServices = new();
        if (registration.CapabilityInterfaceType is { } capabilityInterfaceType)
        {
            object proxy =
                registration.Loader.CreateProxy(capabilityInterfaceType, registration.HttpClient)
                ?? throw new InvalidOperationException(
                    $"'{capabilityInterfaceType}' is no longer one of the loader's available "
                        + "capabilities; the capability assembly may have changed shape."
                );
            perRenderServices.AddSingleton(capabilityInterfaceType, proxy);
        }

        await using ServiceProvider builtPerRenderServices =
            perRenderServices.BuildServiceProvider();
        FallbackServiceProvider scoped = new(builtPerRenderServices, CircuitServices);

        await using HtmlRenderer renderer = new(scoped, LoggerFactory);

        Dictionary<string, object?> parameters = new(StringComparer.Ordinal)
        {
            [nameof(IConfigControl.Control)] = Control,
            [nameof(IConfigControl.Document)] = Document,
            [nameof(IConfigControl.OnFieldChanged)] = OnFieldChanged,
        };

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            HtmlRootComponent root = await renderer.RenderComponentAsync(
                registration.ComponentType,
                ParameterView.FromDictionary(parameters)
            );
            await root.QuiescenceTask.WaitAsync(cancellationToken);
            return root.ToHtmlString();
        });
    }
}
