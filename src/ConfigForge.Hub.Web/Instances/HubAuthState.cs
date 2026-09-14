namespace ConfigForge.Hub.Web.Instances;

/// <summary>
/// The Hub's current own Basic Auth password (protected, never plaintext), shared between the
/// auth middleware (reads it on every request) and <see cref="HubDocumentProvider"/> (updates it
/// the moment a save changes it, mirroring how <see cref="ConfigForge.AspNet.AspNetConfigForgeOptions.RemoteInstances"/>
/// is mutated directly rather than re-read from disk).
/// </summary>
internal sealed class HubAuthState
{
    public string? PasswordProtected { get; set; }
}
