namespace ConfigForge.Abstractions.Annotations;

/// <summary>
/// Declares an action button in the generated schema (<c>x-cf.actions</c>). Placed on the
/// config <b>type</b> and repeatable. The button's behaviour is still supplied in code by
/// registering a handler for the same <see cref="ActionId"/> via
/// <c>IPluginRegistry.RegisterAction</c> — this attribute only declares the button and where
/// it appears.
/// </summary>
/// <example>
/// <code>
/// [CfAction("test-smtp", Label = "Send test email", Category = "Alerts", Icon = "mail")]
/// public sealed record AppConfig { … }
/// </code>
/// </example>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct,
    AllowMultiple = true,
    Inherited = false
)]
public sealed class CfActionAttribute : Attribute
{
    /// <summary>Creates an action declaration.</summary>
    /// <param name="actionId">The id a registered handler is keyed by.</param>
    public CfActionAttribute(string actionId) => ActionId = actionId;

    /// <summary>The action id (matches the handler registered in code).</summary>
    public string ActionId { get; }

    /// <summary>Button label; defaults to the action id when unset.</summary>
    public string? Label { get; init; }

    /// <summary>The category (sidebar group) the button appears in; null = global.</summary>
    public string? Category { get; init; }

    /// <summary>Placement within the category: <c>top</c> or <c>bottom</c> (default).</summary>
    public string Position { get; init; } = "bottom";

    /// <summary>Button style: <c>primary</c>, <c>secondary</c> (default), or <c>danger</c>.</summary>
    public string Variant { get; init; } = "secondary";

    /// <summary>Optional icon identifier.</summary>
    public string? Icon { get; init; }
}
