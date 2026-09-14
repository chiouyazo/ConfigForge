namespace ConfigForge.Hub.Core.Instances;

internal sealed class StoredInstance
{
    public string Name { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;

    public string PathPrefix { get; set; } = "/config-ui";

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Local paths to this instance's capability assemblies - a product commonly splits its
    /// widgets across more than one assembly, so this is a list, not just a single path.
    /// </summary>
    public List<string> CapabilityAssemblyPaths { get; set; } = [];
}
