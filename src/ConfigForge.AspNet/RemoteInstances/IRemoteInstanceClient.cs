using ConfigForge.AspNet.Endpoints;

namespace ConfigForge.AspNet.RemoteInstances;

/// <summary>Speaks the headless dashboard protocol to one <see cref="RemoteInstanceOptions"/>.</summary>
internal interface IRemoteInstanceClient
{
    string InstanceName { get; }

    Task<ManifestResponse> GetManifestAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<CollectionEntryResponse>> GetDataAsync(
        string sectionId,
        CancellationToken cancellationToken
    );

    Task<RemoteActionResult> PostActionAsync(
        string actionId,
        string? entryKey,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Fetches the instance's full raw document JSON (already redacted the same way its own
    /// editor sees it). Returns null when the instance reports it has no document persistence
    /// configured (a 404), rather than throwing.
    /// </summary>
    Task<string?> GetDocumentAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Posts a full document JSON to the instance, running through its real save/validation
    /// pipeline exactly as posting to its own <c>POST document</c> endpoint would.
    /// </summary>
    Task<RemoteDocumentSaveResult> PostDocumentAsync(
        string documentJson,
        CancellationToken cancellationToken
    );
}
