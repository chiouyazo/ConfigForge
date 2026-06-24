namespace ConfigForge.Abstractions;

/// <summary>Severity level of a toast notification.</summary>
public enum ToastSeverity
{
    /// <summary>Informational message.</summary>
    Info,

    /// <summary>Success confirmation.</summary>
    Success,

    /// <summary>Non-blocking warning.</summary>
    Warning,

    /// <summary>Error or failure.</summary>
    Danger,
}
