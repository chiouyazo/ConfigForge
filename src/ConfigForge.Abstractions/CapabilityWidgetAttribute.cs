namespace ConfigForge.Abstractions;

/// <summary>
/// Marks a Blazor component class as the real widget behind a custom control type, discoverable
/// by <c>CapabilityLoader</c> the same way it discovers <see cref="CapabilityContractAttribute"/>
/// interfaces, so a remote aggregator can render the widget without a compile-time reference to
/// the product that owns it.
/// </summary>
/// <remarks>
/// The marked class still implements <c>IConfigControl</c> and still depends on a
/// <see cref="CapabilityContractAttribute"/>-marked interface via an <c>[Inject]</c> property for
/// its data, exactly as it would for local rendering; this attribute only lets a loader find the
/// component <see cref="Type"/> by <see cref="WidgetId"/> without instantiating it.
/// </remarks>
/// <param name="widgetId">
/// The control type identifier a mirrored field's <c>controlType</c> must match for this widget
/// to apply, e.g. <c>"steps-sync.logs.viewer"</c>.
/// </param>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class CapabilityWidgetAttribute(string widgetId) : Attribute
{
    /// <summary>The control type identifier this widget renders.</summary>
    public string WidgetId { get; } = widgetId;
}
