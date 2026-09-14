namespace ConfigForge.AspNet.RemoteInstances;

internal sealed class RemoteInstancePollingCoordinator(
    IRemoteInstanceClientFactory clientFactory,
    IRemoteInstancePoller poller,
    RemoteInstanceStateStore store
) : IRemoteInstancePollingCoordinator
{
    public Task RefreshAllAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<string> names = clientFactory.InstanceNames;
        store.Prune(names);
        return Task.WhenAll(names.Select(name => RefreshOneAsync(name, cancellationToken)));
    }

    private async Task RefreshOneAsync(string instanceName, CancellationToken cancellationToken)
    {
        IRemoteInstanceClient client = clientFactory.Create(instanceName);
        RemoteInstanceSnapshot? previous = store.TryGet(instanceName);
        RemoteInstanceSnapshot snapshot = await poller.PollAsync(
            client,
            previous,
            cancellationToken
        );
        store.Update(snapshot);
    }
}
