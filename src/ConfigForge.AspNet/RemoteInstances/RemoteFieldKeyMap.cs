namespace ConfigForge.AspNet.RemoteInstances;

/// <summary>
/// Thread-safe last-known map from every composite field/collection key currently mirrored into
/// the local schema back to the instance and original key it came from. Rebuilt by
/// <see cref="RemoteSchemaAggregator.BuildContent"/> after every poll cycle, alongside
/// <see cref="IConfigForgeHostState.SetRemoteContent"/>. Used by <see cref="RemoteDocumentMergeService"/>
/// to overlay remote document values onto a locally loaded document, and to split a saved merged
/// document back into per-origin sub-documents.
/// </summary>
internal sealed class RemoteFieldKeyMap
{
    private readonly object _gate = new();
    private IReadOnlyDictionary<string, RemoteFieldOrigin> _map = new Dictionary<
        string,
        RemoteFieldOrigin
    >(StringComparer.Ordinal);

    public void Replace(IReadOnlyDictionary<string, RemoteFieldOrigin> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        lock (_gate)
        {
            _map = map;
        }
    }

    public IReadOnlyDictionary<string, RemoteFieldOrigin> Current
    {
        get
        {
            lock (_gate)
            {
                return _map;
            }
        }
    }
}
