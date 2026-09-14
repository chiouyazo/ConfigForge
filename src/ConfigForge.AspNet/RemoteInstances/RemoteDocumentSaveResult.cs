using ConfigForge.AspNet.Endpoints;

namespace ConfigForge.AspNet.RemoteInstances;

/// <summary>The outcome of posting a document to one remote instance's <c>POST document</c> endpoint.</summary>
internal sealed record RemoteDocumentSaveResult(
    bool Success,
    string? Error,
    IReadOnlyList<string> MissingRequiredKeys,
    IReadOnlyList<ValidationErrorResponse> InvalidValues
)
{
    public static readonly RemoteDocumentSaveResult Ok = new(true, null, [], []);

    public static RemoteDocumentSaveResult Rejected(DocumentSaveErrorResponse body) =>
        new(false, body.Error, body.MissingRequiredKeys, body.InvalidValues);

    public static RemoteDocumentSaveResult Failed(string error) => new(false, error, [], []);
}
