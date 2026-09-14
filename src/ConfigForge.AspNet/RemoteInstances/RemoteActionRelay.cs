namespace ConfigForge.AspNet.RemoteInstances;

internal sealed class RemoteActionRelay(IRemoteInstanceClientFactory clientFactory)
    : IRemoteActionRelay
{
    public Task<RemoteActionResult> InvokeAsync(
        string instanceName,
        string actionId,
        string? entryKey,
        CancellationToken cancellationToken
    )
    {
        IRemoteInstanceClient client = clientFactory.Create(instanceName);
        return client.PostActionAsync(actionId, entryKey, cancellationToken);
    }
}
