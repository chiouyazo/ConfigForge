namespace ConfigForge.AspNet.Endpoints;

/// <summary>One field of the manifest.</summary>
/// <param name="Key">The field's path key, relative to its container.</param>
/// <param name="ControlType">The resolved control type.</param>
/// <param name="Title">The field title, if any.</param>
/// <param name="Required">Whether the field is required.</param>
/// <param name="ReadOnly">Whether the field is read-only at the origin.</param>
public sealed record ManifestField(
    string Key,
    string ControlType,
    string? Title,
    bool Required,
    bool ReadOnly = false
);
