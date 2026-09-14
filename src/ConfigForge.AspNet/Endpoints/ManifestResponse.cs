namespace ConfigForge.AspNet.Endpoints;

/// <summary>The resolved schema, as served by the manifest endpoint.</summary>
/// <param name="SchemaId">The schema identifier.</param>
/// <param name="Name">The schema display name.</param>
/// <param name="Version">The schema version.</param>
/// <param name="Categories">The schema's categories.</param>
/// <param name="Actions">Every action declared anywhere in the schema.</param>
public sealed record ManifestResponse(
    string SchemaId,
    string? Name,
    string? Version,
    IReadOnlyList<ManifestCategory> Categories,
    IReadOnlyList<ManifestAction> Actions
);
