using ConfigForge.Abstractions;

namespace ConfigForge.Plugin.CapabilityFixture;

/// <summary>A capability contract used only by <c>CapabilityLoader</c>'s tests.</summary>
[CapabilityContract]
public interface IFixtureCapability
{
    /// <summary>Returns <paramref name="value"/> unchanged. A "real" implementation would hit real state.</summary>
    Task<string> EchoAsync(string value, CancellationToken cancellationToken = default);
}
