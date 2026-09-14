namespace ConfigForge.AspNet.Endpoints;

/// <summary>
/// The structured error body of a rejected <c>POST {prefix}/document</c>, mirroring the same
/// validation the interactive editor's save button would have blocked on.
/// </summary>
/// <param name="Error">A short human-readable summary.</param>
/// <param name="JsonError">Set when the posted body was not well-formed JSON at all.</param>
/// <param name="MissingRequiredKeys">Required field keys absent from the posted document.</param>
/// <param name="InvalidValues">Present fields whose value failed schema-constraint validation.</param>
public sealed record DocumentSaveErrorResponse(
    string Error,
    string? JsonError,
    IReadOnlyList<string> MissingRequiredKeys,
    IReadOnlyList<ValidationErrorResponse> InvalidValues
);
