namespace ConfigForge.Blazor.Components.Fields;

/// <summary>
/// An <see cref="IServiceProvider"/> that tries a small, per-render <paramref name="primary"/>
/// provider first (holding exactly one instance's capability proxy) and falls back to the
/// hosting circuit's own <paramref name="fallback"/> provider for everything else a widget's
/// <c>[Inject]</c> properties need (loggers, JS interop, navigation, ...).
/// </summary>
internal sealed class FallbackServiceProvider(IServiceProvider primary, IServiceProvider fallback)
    : IServiceProvider
{
    public object? GetService(Type serviceType) =>
        primary.GetService(serviceType) ?? fallback.GetService(serviceType);
}
