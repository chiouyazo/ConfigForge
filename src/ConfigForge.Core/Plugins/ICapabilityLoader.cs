namespace ConfigForge.Core.Plugins;

/// <summary>
/// Loads a plugin assembly for its <c>[CapabilityContract]</c>-marked interfaces without ever
/// instantiating or invoking a concrete type from it, and builds HTTP-calling proxies for them.
/// </summary>
public interface ICapabilityLoader
{
    /// <summary>The capability interfaces currently available.</summary>
    IReadOnlyCollection<Type> AvailableCapabilities { get; }

    /// <summary>
    /// The widget component types currently available, keyed by their
    /// <see cref="CapabilityWidgetAttribute.WidgetId"/>.
    /// </summary>
    IReadOnlyDictionary<string, Type> AvailableWidgets { get; }

    /// <summary>
    /// Loads (or reloads) the assembly at <paramref name="assemblyPath"/> and enumerates its
    /// <c>[CapabilityContract]</c> interfaces. Never runs any code from the assembly: it only
    /// reflects over its types.
    /// </summary>
    /// <param name="assemblyPath">The path to the plugin assembly.</param>
    /// <returns>
    /// The load outcome. A missing file or a load failure leaves <see cref="AvailableCapabilities"/>
    /// exactly as it was before the call: this method never clears state on a transient failure.
    /// </returns>
    CapabilityLoadResult Load(string assemblyPath);

    /// <summary>
    /// Builds a remote-calling proxy for <paramref name="capabilityInterface"/>, or
    /// <see langword="null"/> if it is not one of <see cref="AvailableCapabilities"/>.
    /// </summary>
    /// <param name="capabilityInterface">One of the <see cref="AvailableCapabilities"/>.</param>
    /// <param name="httpClient">
    /// The client used for every call the proxy makes. Its <see cref="HttpClient.BaseAddress"/>
    /// must be the host's <c>PathPrefix</c> (ending in <c>/</c>); credentials and message handler
    /// configuration are the caller's responsibility.
    /// </param>
    object? CreateProxy(Type capabilityInterface, HttpClient httpClient);

    /// <summary>Typed convenience wrapper over <see cref="CreateProxy(Type, HttpClient)"/>.</summary>
    /// <typeparam name="T">The capability interface to proxy.</typeparam>
    /// <param name="httpClient">See <see cref="CreateProxy(Type, HttpClient)"/>.</param>
    T? CreateProxy<T>(HttpClient httpClient)
        where T : class;
}
