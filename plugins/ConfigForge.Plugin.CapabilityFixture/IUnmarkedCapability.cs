namespace ConfigForge.Plugin.CapabilityFixture;

/// <summary>An interface deliberately NOT marked <c>[CapabilityContract]</c>, for negative tests.</summary>
public interface IUnmarkedCapability
{
    /// <summary>Never expected to be reachable through a capability loader.</summary>
    Task<string> EchoAsync(string value);
}
