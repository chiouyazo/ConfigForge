namespace ConfigForge.Core.Plugins;

/// <summary>The result of loading (or attempting to reload) a capability assembly.</summary>
/// <param name="Status">Whether the load succeeded, or why not.</param>
/// <param name="Capabilities">
/// The capability interfaces currently available after this call: the freshly loaded set on
/// <see cref="CapabilityLoadStatus.Loaded"/>, or whatever was already loaded on any other status.
/// </param>
/// <param name="Error">The exception that caused a <see cref="CapabilityLoadStatus.LoadFailed"/>, if any.</param>
/// <param name="Widgets">
/// The widget component types currently available after this call, keyed by widget id; mirrors
/// <paramref name="Capabilities"/>'s same-status semantics.
/// </param>
public sealed record CapabilityLoadResult(
    CapabilityLoadStatus Status,
    IReadOnlyCollection<Type> Capabilities,
    Exception? Error,
    IReadOnlyDictionary<string, Type> Widgets
)
{
    /// <summary>Convenience constructor for a result with no widgets, e.g. one built by tests.</summary>
    public CapabilityLoadResult(
        CapabilityLoadStatus status,
        IReadOnlyCollection<Type> capabilities,
        Exception? error = null
    )
        : this(status, capabilities, error, new Dictionary<string, Type>(StringComparer.Ordinal))
    { }
}
