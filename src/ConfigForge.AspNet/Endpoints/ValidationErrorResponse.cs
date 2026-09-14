namespace ConfigForge.AspNet.Endpoints;

/// <summary>One field-level validation failure, as served in <see cref="DocumentSaveErrorResponse"/>.</summary>
/// <param name="Key">The key of the field that failed validation.</param>
/// <param name="Message">A human-readable description of the failure.</param>
public sealed record ValidationErrorResponse(string Key, string Message);
