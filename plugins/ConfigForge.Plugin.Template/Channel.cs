namespace ConfigForge.Plugin.Template;

/// <summary>
/// A channel returned by the example service. Stub support type so the
/// reference handler code compiles without any external dependency.
/// </summary>
/// <param name="Id">The channel identifier, written to the config document.</param>
/// <param name="Name">The human-readable channel name shown to the user.</param>
public sealed record Channel(string Id, string Name);
