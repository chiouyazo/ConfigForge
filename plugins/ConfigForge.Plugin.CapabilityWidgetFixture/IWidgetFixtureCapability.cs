using ConfigForge.Abstractions;

namespace ConfigForge.Plugin.CapabilityWidgetFixture;

/// <summary>A capability contract used only by the capability-widget-hosting tests.</summary>
[CapabilityContract]
public interface IWidgetFixtureCapability
{
    /// <summary>Returns a small list of entries. A "real" implementation would hit real state.</summary>
    Task<IReadOnlyList<string>> GetEntriesAsync(CancellationToken cancellationToken = default);
}
