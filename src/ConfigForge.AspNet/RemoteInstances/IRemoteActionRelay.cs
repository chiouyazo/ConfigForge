namespace ConfigForge.AspNet.RemoteInstances;

internal interface IRemoteActionRelay
{
    Task<RemoteActionResult> InvokeAsync(
        string instanceName,
        string actionId,
        string? entryKey,
        CancellationToken cancellationToken
    );
}
