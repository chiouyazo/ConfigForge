namespace ConfigForge.Abstractions;

/// <summary>Result returned by a plugin validator.</summary>
public sealed class ValidationResult
{
    /// <summary>True when validation passed.</summary>
    public bool IsValid { get; init; }

    /// <summary>Validation message shown to the user when IsValid is false.</summary>
    public string? Message { get; init; }

    /// <summary>Returns a passing result.</summary>
    /// <returns>A passing <see cref="ValidationResult"/>.</returns>
    public static ValidationResult Ok() => new() { IsValid = true };

    /// <summary>Returns a failing result with the given message.</summary>
    /// <param name="message">The message describing the failure.</param>
    /// <returns>A failing <see cref="ValidationResult"/>.</returns>
    public static ValidationResult Fail(string message) =>
        new() { IsValid = false, Message = message };
}
