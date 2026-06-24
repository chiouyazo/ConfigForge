namespace ConfigForge.Core.Schema;

/// <summary>
/// An action button declared in <c>x-cf.actions</c>. The button is rendered by the
/// UI; its behaviour is supplied by a plugin that registers a handler for the same
/// <see cref="ActionId"/> via <c>IPluginRegistry.RegisterAction</c>. Actions are not
/// built in; a button does nothing unless a plugin handles its id.
/// </summary>
public sealed class ActionDefinition
{
    /// <summary>The action identifier, matched to a plugin-registered handler.</summary>
    public string ActionId { get; init; } = string.Empty;

    /// <summary>The button label.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Optional icon identifier resolved against the configured icon set.</summary>
    public string? Icon { get; init; }

    /// <summary>Visual variant: <c>primary</c>, <c>secondary</c>, or <c>danger</c>.</summary>
    public string Variant { get; init; } = "secondary";

    /// <summary>The category label this action is placed in, or null for all.</summary>
    public string? Category { get; init; }

    /// <summary>Placement within the category: <c>top</c> or <c>bottom</c>.</summary>
    public string Position { get; init; } = "bottom";
}
