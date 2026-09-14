namespace ConfigForge.AspNet.RemoteInstances;

/// <summary>Creates an <see cref="IRemoteInstanceClient"/> for a configured instance name.</summary>
internal interface IRemoteInstanceClientFactory
{
    IRemoteInstanceClient Create(string instanceName);

    IReadOnlyList<string> InstanceNames { get; }
}
