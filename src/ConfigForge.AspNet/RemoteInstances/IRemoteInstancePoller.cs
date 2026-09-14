namespace ConfigForge.AspNet.RemoteInstances;

internal interface IRemoteInstancePoller
{
    Task<RemoteInstanceSnapshot> PollAsync(
        IRemoteInstanceClient client,
        RemoteInstanceSnapshot? previous,
        CancellationToken cancellationToken
    );
}
