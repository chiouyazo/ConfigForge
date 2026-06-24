using ConfigForge.Abstractions;

namespace ConfigForge.Blazor.Services;

/// <summary>A single queued toast notification.</summary>
public sealed class ToastMessage
{
    /// <summary>A process-unique identifier used as the render key.</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>The message text shown to the user.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>The severity that drives the toast styling.</summary>
    public ToastSeverity Severity { get; init; }
}
