using ConfigForge.Abstractions;
using ConfigForge.Core.Schema;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConfigForge.AspNet.RemoteInstances;

/// <summary>
/// Periodically polls every configured <see cref="AspNetConfigForgeOptions.RemoteInstances"/>
/// entry and merges the resulting categories/fields/actions into every schema the host has
/// registered, via <see cref="IConfigForgeHostState.SetRemoteContent"/>. Registered only when
/// <see cref="AspNetConfigForgeOptions.RemoteInstances"/> is non-empty.
/// </summary>
// CA1812: instantiated by the DI container via AddHostedService<T>, which the analyzer cannot see.
#pragma warning disable CA1812
internal sealed partial class RemoteInstancesHostedService(
    IRemoteInstancePollingCoordinator coordinator,
    RemoteInstanceStateStore stateStore,
    IPluginRegistry pluginRegistry,
    IRemoteActionRelay actionRelay,
    IConfigForgeHostState hostState,
    RemoteFieldKeyMap fieldKeyMap,
    CapabilityWidgetCatalog capabilityWidgets,
    AspNetConfigForgeOptions options,
    ILogger<RemoteInstancesHostedService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeSpan interval = TimeSpan.FromSeconds(
            Math.Max(1, options.RemoteInstancePollIntervalSeconds)
        );

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await coordinator.RefreshAllAsync(stoppingToken);
                Sync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogPollingCycleFailed(logger, ex);
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void Sync()
    {
        capabilityWidgets.Refresh(options.RemoteInstances);

        IReadOnlyList<RemoteInstanceSnapshot> snapshots = stateStore.All();
        (
            IReadOnlyList<CategoryElement> categories,
            IReadOnlyDictionary<string, FieldDefinition> fields,
            IReadOnlyList<ActionDefinition> actions,
            IReadOnlyDictionary<string, RemoteFieldOrigin> fieldOrigins
        ) = RemoteSchemaAggregator.BuildContent(snapshots, capabilityWidgets);

        hostState.SetRemoteContent(categories, fields, actions);
        fieldKeyMap.Replace(fieldOrigins);
        RemoteSchemaAggregator.RegisterHandlers(pluginRegistry, stateStore, actionRelay, snapshots);
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Remote instance polling cycle failed unexpectedly."
    )]
    private static partial void LogPollingCycleFailed(ILogger logger, Exception exception);
}
#pragma warning restore CA1812
