namespace ConfigForge.Abstractions;

/// <summary>
/// Conventions for write-only secret fields (control type <c>secret</c>).
/// </summary>
/// <remarks>
/// A host that stores secrets encrypted or hashed replaces the real value with
/// <see cref="StoredMarker"/> when it loads a document, so the plaintext never
/// reaches the browser. On save it interprets the field: <see cref="StoredMarker"/>
/// means "keep the stored secret unchanged", a non-empty value means "replace with
/// this new plaintext", and an empty string or null means "clear the secret".
/// </remarks>
public static class ConfigForgeSecret
{
    /// <summary>
    /// Sentinel value marking that a secret is stored but its value is deliberately
    /// withheld from the editor. Distinctive enough that no real secret collides.
    /// </summary>
    public const string StoredMarker = "__configforge-secret-stored__";
}
