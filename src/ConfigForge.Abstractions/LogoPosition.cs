namespace ConfigForge.Abstractions;

/// <summary>Defines where a logo is placed within the ConfigForge shell layout.</summary>
public enum LogoPosition
{
    /// <summary>Top-left corner of the main header bar.</summary>
    TopLeft,

    /// <summary>Centered in the main header bar.</summary>
    TopCenter,

    /// <summary>Top-right corner of the main header bar.</summary>
    TopRight,

    /// <summary>Top of the left sidebar, above category navigation.</summary>
    SidebarTop,

    /// <summary>Bottom of the left sidebar, below category navigation.</summary>
    SidebarBottom,
}
