namespace ConfigForge.AspNet.RemoteInstances;

/// <summary>Thread-safe last-known-state cache for every configured remote instance.</summary>
internal sealed class RemoteInstanceStateStore
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<
        string,
        RemoteInstanceSnapshot
    > _snapshots = new(StringComparer.Ordinal);

    public void Update(RemoteInstanceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _snapshots[snapshot.InstanceName] = snapshot;
    }

    public RemoteInstanceSnapshot? TryGet(string instanceName) =>
        _snapshots.TryGetValue(instanceName, out RemoteInstanceSnapshot? snapshot)
            ? snapshot
            : null;

    public IReadOnlyList<RemoteInstanceSnapshot> All() =>
        [.. _snapshots.Values.OrderBy(s => s.InstanceName, StringComparer.Ordinal)];

    public void Prune(IReadOnlyCollection<string> validInstanceNames)
    {
        ArgumentNullException.ThrowIfNull(validInstanceNames);
        HashSet<string> valid = new(validInstanceNames, StringComparer.Ordinal);
        foreach (string name in _snapshots.Keys)
        {
            if (!valid.Contains(name))
            {
                _snapshots.TryRemove(name, out _);
            }
        }
    }
}
