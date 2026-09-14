namespace ConfigForge.AspNet.RemoteInstances;

/// <summary>The outcome of splitting a merged document save across every changed origin.</summary>
/// <param name="Success">False when at least one origin rejected its share of the save.</param>
/// <param name="Failures">The per-origin failures, when <see cref="Success"/> is false.</param>
internal sealed record RemoteDocumentSaveOutcome(
    bool Success,
    IReadOnlyList<RemoteDocumentSaveFailure> Failures
)
{
    public static readonly RemoteDocumentSaveOutcome Ok = new(true, []);

    public static RemoteDocumentSaveOutcome Failed(
        IReadOnlyList<RemoteDocumentSaveFailure> failures
    ) => new(false, failures);
}
