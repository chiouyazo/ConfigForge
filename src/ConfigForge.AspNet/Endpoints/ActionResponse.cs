namespace ConfigForge.AspNet.Endpoints;

/// <summary>The response body of the action endpoint, mirroring what the interactive UI would show.</summary>
/// <param name="Success">
/// False when the handler reported failure (its last toast was a warning or danger severity).
/// </param>
/// <param name="Message">The last toast message the handler raised, if any.</param>
/// <param name="Severity">The last toast severity the handler raised, if any.</param>
public sealed record ActionResponse(bool Success, string? Message, string? Severity);
