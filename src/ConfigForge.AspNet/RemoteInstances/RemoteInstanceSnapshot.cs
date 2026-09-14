using ConfigForge.AspNet.Endpoints;

namespace ConfigForge.AspNet.RemoteInstances;

internal enum RemoteInstanceStatus
{
    Online,
    Unauthorized,
    Offline,
}

/// <summary>
/// The distinguishable cause of a failed poll, when <see cref="RemoteInstanceSnapshot.Status"/> is
/// <see cref="RemoteInstanceStatus.Offline"/>. Sourced from the kind of exception
/// <see cref="RemoteInstancePoller"/> caught, not inferred after the fact.
/// </summary>
internal enum RemoteInstanceFailureReason
{
    None,
    ConnectionFailed,
    TimedOut,
    HttpError,
}

/// <summary>The last-known state of one polled remote instance.</summary>
internal sealed record RemoteInstanceSnapshot(
    string InstanceName,
    RemoteInstanceStatus Status,
    DateTimeOffset LastPolledAt,
    string? ErrorMessage,
    RemoteInstanceFailureReason FailureReason,
    int? HttpStatusCode,
    ManifestResponse? Manifest,
    IReadOnlyDictionary<string, IReadOnlyList<CollectionEntryResponse>> SectionData,
    string? DocumentJson = null
)
{
    public static RemoteInstanceSnapshot Online(
        string instanceName,
        DateTimeOffset polledAt,
        ManifestResponse manifest,
        IReadOnlyDictionary<string, IReadOnlyList<CollectionEntryResponse>> sectionData,
        string? documentJson = null
    ) =>
        new(
            instanceName,
            RemoteInstanceStatus.Online,
            polledAt,
            null,
            RemoteInstanceFailureReason.None,
            null,
            manifest,
            sectionData,
            documentJson
        );

    public static RemoteInstanceSnapshot Unreachable(
        string instanceName,
        DateTimeOffset polledAt,
        string errorMessage,
        RemoteInstanceFailureReason failureReason,
        int? httpStatusCode,
        RemoteInstanceSnapshot? previous
    ) =>
        new(
            instanceName,
            RemoteInstanceStatus.Offline,
            polledAt,
            errorMessage,
            failureReason,
            httpStatusCode,
            previous?.Manifest,
            previous?.SectionData
                ?? new Dictionary<string, IReadOnlyList<CollectionEntryResponse>>(
                    StringComparer.Ordinal
                ),
            previous?.DocumentJson
        );

    public static RemoteInstanceSnapshot Unauthorized(
        string instanceName,
        DateTimeOffset polledAt,
        RemoteInstanceSnapshot? previous
    ) =>
        new(
            instanceName,
            RemoteInstanceStatus.Unauthorized,
            polledAt,
            "The instance rejected the configured credentials.",
            RemoteInstanceFailureReason.None,
            null,
            previous?.Manifest,
            previous?.SectionData
                ?? new Dictionary<string, IReadOnlyList<CollectionEntryResponse>>(
                    StringComparer.Ordinal
                ),
            previous?.DocumentJson
        );
}
