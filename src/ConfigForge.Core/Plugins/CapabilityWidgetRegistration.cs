namespace ConfigForge.Core.Plugins;

/// <summary>
/// One widget resolved for rendering: the component <see cref="Type"/> a mirrored control type
/// maps to, the <c>[CapabilityContract]</c> interface it depends on, and the loader/client pair
/// that builds a fresh HTTP-calling proxy for that interface, scoped to the exact remote instance
/// the widget must show data from.
/// </summary>
/// <param name="ComponentType">The widget's <c>IConfigControl</c> component type.</param>
/// <param name="CapabilityInterfaceType">
/// The single <c>[CapabilityContract]</c> interface the widget injects for its data, or
/// <see langword="null"/> for a widget with no such dependency (e.g. one that only renders
/// content baked into the capability assembly itself, like a license report).
/// </param>
/// <param name="Loader">The loader that discovered <paramref name="ComponentType"/>.</param>
/// <param name="HttpClient">
/// The client to call through for this specific remote instance; never shared across instances.
/// </param>
public sealed record CapabilityWidgetRegistration(
    Type ComponentType,
    Type? CapabilityInterfaceType,
    ICapabilityLoader Loader,
    HttpClient HttpClient
);
