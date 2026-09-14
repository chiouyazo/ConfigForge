using ConfigForge.AspNet.Endpoints;

namespace ConfigForge.AspNet.RemoteInstances;

/// <summary>One origin's rejection of its share of a split document save.</summary>
/// <param name="InstanceName">The origin instance that rejected the save.</param>
/// <param name="Error">A short human-readable summary.</param>
/// <param name="MissingRequiredKeys">Required keys the origin reported missing, if any.</param>
/// <param name="InvalidValues">Field-level validation failures the origin reported, if any.</param>
internal sealed record RemoteDocumentSaveFailure(
    string InstanceName,
    string Error,
    IReadOnlyList<string> MissingRequiredKeys,
    IReadOnlyList<ValidationErrorResponse> InvalidValues
);
