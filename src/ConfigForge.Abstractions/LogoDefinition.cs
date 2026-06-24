namespace ConfigForge.Abstractions;

/// <summary>Defines a logo image displayed within the ConfigForge UI.</summary>
public sealed class LogoDefinition
{
    /// <summary>Base64-encoded image data without the data URI prefix.</summary>
    public string Base64Data { get; init; } = string.Empty;

    /// <summary>MIME type of the image, e.g. image/svg+xml or image/png.</summary>
    public string MimeType { get; init; } = "image/svg+xml";

    /// <summary>Accessible alt text for the logo image.</summary>
    public string AltText { get; init; } = string.Empty;

    /// <summary>Where in the layout the logo is rendered.</summary>
    public LogoPosition Position { get; init; } = LogoPosition.SidebarTop;
}
