namespace ConfigForge.AspNet.Endpoints;

/// <summary>One entry of a section's data, as served by the data endpoint.</summary>
/// <param name="Key">The entry key: a map key for a document-backed collection, or a synthetic
/// index for a loader-backed one.</param>
/// <param name="Value">The entry's field values.</param>
public sealed record CollectionEntryResponse(string Key, object? Value);
