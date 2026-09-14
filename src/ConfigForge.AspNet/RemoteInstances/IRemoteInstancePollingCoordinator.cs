namespace ConfigForge.AspNet.RemoteInstances;

internal interface IRemoteInstancePollingCoordinator
{
    Task RefreshAllAsync(CancellationToken cancellationToken);
}
