using System.Text.Json;
using System.Text.Json.Serialization;
using ConfigForge.Abstractions;

namespace ConfigForge.AspNet;

/// <summary>
/// Loads an optional <c>theme.json</c> override from the content root and produces
/// a <see cref="ThemeDefinition"/>. The file shape is
/// <c>{ primaryColor, fontFamily, borderRadius, logo: { path, mimeType, altText, position } }</c>.
/// When a logo <c>path</c> is supplied the referenced file is read and base64-encoded.
/// </summary>
public static class ThemeJsonLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Attempts to load <c>theme.json</c> from <paramref name="contentRootPath"/>
    /// and convert it to a <see cref="ThemeDefinition"/>.
    /// </summary>
    /// <param name="contentRootPath">The application content root directory.</param>
    /// <param name="theme">The resolved theme when the file exists and parses.</param>
    /// <returns>
    /// <see langword="true"/> when a <c>theme.json</c> file was found and converted;
    /// otherwise <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="contentRootPath"/> is null or empty.</exception>
    public static bool TryLoad(string contentRootPath, out ThemeDefinition? theme)
    {
        ArgumentException.ThrowIfNullOrEmpty(contentRootPath);

        theme = null;
        string themePath = Path.Combine(contentRootPath, "theme.json");
        if (!File.Exists(themePath))
        {
            return false;
        }

        string json = File.ReadAllText(themePath);
        ThemeFile? parsed = JsonSerializer.Deserialize<ThemeFile>(json, SerializerOptions);
        if (parsed is null)
        {
            return false;
        }

        theme = parsed.ToThemeDefinition(contentRootPath);
        return true;
    }

    /// <summary>The deserialized shape of a <c>theme.json</c> file.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "Instantiated by System.Text.Json deserialization via reflection."
    )]
    private sealed class ThemeFile
    {
        [JsonPropertyName("primaryColor")]
        public string? PrimaryColor { get; set; }

        [JsonPropertyName("fontFamily")]
        public string? FontFamily { get; set; }

        [JsonPropertyName("borderRadius")]
        public string? BorderRadius { get; set; }

        [JsonPropertyName("logo")]
        public LogoFile? Logo { get; set; }

        public ThemeDefinition ToThemeDefinition(string contentRootPath)
        {
            var defaults = new ThemeDefinition();
            return new ThemeDefinition
            {
                PrimaryColor = PrimaryColor ?? defaults.PrimaryColor,
                FontFamily = FontFamily ?? defaults.FontFamily,
                BorderRadius = BorderRadius ?? defaults.BorderRadius,
                Logo = Logo?.ToLogoDefinition(contentRootPath),
            };
        }
    }

    /// <summary>The deserialized shape of the optional <c>logo</c> section.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "Instantiated by System.Text.Json deserialization via reflection."
    )]
    private sealed class LogoFile
    {
        [JsonPropertyName("path")]
        public string? Path { get; set; }

        [JsonPropertyName("mimeType")]
        public string? MimeType { get; set; }

        [JsonPropertyName("altText")]
        public string? AltText { get; set; }

        [JsonPropertyName("position")]
        public string? Position { get; set; }

        public LogoDefinition? ToLogoDefinition(string contentRootPath)
        {
            if (string.IsNullOrEmpty(Path))
            {
                return null;
            }

            string logoPath = System.IO.Path.IsPathRooted(Path)
                ? Path
                : System.IO.Path.Combine(contentRootPath, Path);

            if (!File.Exists(logoPath))
            {
                return null;
            }

            byte[] bytes = File.ReadAllBytes(logoPath);
            var defaults = new LogoDefinition();
            return new LogoDefinition
            {
                Base64Data = Convert.ToBase64String(bytes),
                MimeType = MimeType ?? defaults.MimeType,
                AltText = AltText ?? defaults.AltText,
                Position = ParsePosition(Position),
            };
        }

        private static LogoPosition ParsePosition(string? value) =>
            Enum.TryParse(value, ignoreCase: true, out LogoPosition position)
                ? position
                : LogoPosition.SidebarTop;
    }
}
