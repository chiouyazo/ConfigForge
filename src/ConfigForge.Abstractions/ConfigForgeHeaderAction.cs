namespace ConfigForge.Abstractions;

/// <summary>
/// A custom link rendered in the ConfigForge header's action area (where the host can
/// place its own buttons, e.g. help or support links). Kept deliberately generic so any
/// host can supply its own.
/// </summary>
public sealed class ConfigForgeHeaderAction
{
    /// <summary>The visible button text.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>The link target.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1056:URI-like properties should not be strings",
        Justification = "Rendered directly into an anchor href; a string is the natural host input."
    )]
    public string Url { get; init; } = string.Empty;

    /// <summary>When true, the link opens in a new browser tab.</summary>
    public bool OpenInNewTab { get; init; }

    /// <summary>
    /// Optional inline SVG markup. When set, the action renders as an icon-only button
    /// (the <see cref="Label"/> becomes its tooltip/accessible name) instead of a text
    /// button. The host owns the markup; it is rendered verbatim.
    /// </summary>
    public string? IconSvg { get; init; }

    /// <summary>Visual variant: <c>secondary</c> (default) or <c>primary</c>.</summary>
    public string Variant { get; init; } = "secondary";
}
