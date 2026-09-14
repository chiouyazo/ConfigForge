using ConfigForge.AspNet.RemoteInstances;

namespace ConfigForge.Hub.Core.Instances;

internal static class StoredInstanceMapper
{
#pragma warning disable S1075 // sentinel for a not-yet-configured instance, not a real endpoint
    private const string UnsetBaseUrl = "http://unset.invalid";
#pragma warning restore S1075

    public static RemoteInstanceOptions ToRemoteInstanceOptions(StoredInstance stored) =>
        new()
        {
            Name = stored.Name,
            BaseUrl = new Uri(
                string.IsNullOrWhiteSpace(stored.BaseUrl) ? UnsetBaseUrl : stored.BaseUrl
            ),
            PathPrefix = string.IsNullOrWhiteSpace(stored.PathPrefix)
                ? "/config-ui"
                : stored.PathPrefix,
            Username = stored.Username,
            Password = stored.Password,
            CapabilityAssemblyPaths = stored.CapabilityAssemblyPaths,
        };
}
