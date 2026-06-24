# Theming

The UI is styled with CSS variables. A theme sets the values; every component reads only the variables, so changing the theme restyles everything.

## Theme tokens

Implement `IThemeProvider` and register it:

```csharp
public sealed class MyTheme : IThemeProvider
{
    public ThemeDefinition GetTheme() => new()
    {
        PrimaryColor = "#005fa3",
        BackgroundColor = "#f4f5f7",
        FontFamily = "'Inter', sans-serif",
        BorderRadius = "6px",
        // ... see ThemeDefinition for the full set
    };
}
```

In Open mode, set `options.ThemeProvider = new MyTheme()`. Register it before the ConfigForge services if you're wiring DI yourself.

`ThemeDefinition` covers the accent/surface/background/text colors, danger/success/warning colors, font family, border radius, and an optional logo.

## theme.json (Open mode)

Instead of code, drop a `theme.json` in the content root to override the basics without rebuilding:

```json
{
  "primaryColor": "#005fa3",
  "fontFamily": "'Inter', sans-serif",
  "borderRadius": "6px",
  "logo": { "path": "logo.svg", "altText": "My Product", "position": "SidebarTop" }
}
```

## CSS

Tokens become CSS custom properties on the root element:

```
--cf-primary, --cf-surface, --cf-background, --cf-text-primary,
--cf-text-secondary, --cf-danger, --cf-success, --cf-warning,
--cf-font, --cf-radius
```

For anything beyond the tokens, override `configforge.css` (the stylesheet shipped with `ConfigForge.Blazor`). Every component uses `cf-*` classes, so you can target them directly.

Icons are class hooks, not baked-in glyphs: a category or action icon named `link` renders as `<span class="cf-icon-link">`. Supply your own icon font or CSS rules for those classes.
