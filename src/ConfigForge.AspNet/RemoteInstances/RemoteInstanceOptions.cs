namespace ConfigForge.AspNet.RemoteInstances;

/// <summary>
/// One other ConfigForge-hosted instance to poll and merge into this host's schema(s), via
/// <see cref="AspNetConfigForgeOptions.RemoteInstances"/>. Polled over the headless dashboard
/// protocol (<c>/manifest</c>, <c>/data/{sectionId}</c>, <c>/action/{actionId}</c>) documented in
/// <c>docs/dashboard-protocol.md</c>, using this instance's own credentials.
/// </summary>
public sealed class RemoteInstanceOptions
{
    /// <summary>
    /// The instance's display name. Also its namespace: every category and action mirrored from
    /// this instance is identified internally by a value derived from this name, so it must be
    /// unique among the configured <see cref="AspNetConfigForgeOptions.RemoteInstances"/> entries.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>The instance's origin, e.g. <c>https://instance-a.example.com</c>.</summary>
    public required Uri BaseUrl { get; set; }

    /// <summary>The instance's configured <see cref="AspNetConfigForgeOptions.PathPrefix"/>.</summary>
    public string PathPrefix { get; set; } = "/config-ui";

    /// <summary>The Basic Auth username to poll and relay with, if the instance requires one.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>The Basic Auth password to poll and relay with, if the instance requires one.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// The local, pre-deployed paths to this instance's capability assemblies, if it exposes any
    /// custom widget fields (<c>docs/capability-loading.md</c>). A product commonly splits its
    /// widgets across more than one assembly (its own plus any it shares with other products), so
    /// every widget-bearing assembly must be listed here, not just one. Each is loaded via its own
    /// <c>ConfigForge.Core.Plugins.CapabilityLoader</c>, the same controlled deployment step that
    /// mechanism already requires; never fetched from the instance itself over the network. Empty
    /// when this instance has no widget fields to mirror.
    /// </summary>
    public IReadOnlyList<string> CapabilityAssemblyPaths { get; set; } = [];
}
