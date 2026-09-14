using ConfigForge.Abstractions;
using ConfigForge.AspNet.Endpoints;
using ConfigForge.Core.Documents;
using ConfigForge.Core.Schema;

namespace ConfigForge.AspNet.RemoteInstances;

/// <summary>
/// Overlays remote instances' real document values onto a locally loaded document (so a mirrored
/// plain category or document-backed collection shows real data, not just an empty shell declared
/// by the schema), and splits a saved merged document back into one real per-origin document save
/// per changed instance, using that instance's own credentials.
/// </summary>
/// <remarks>
/// Both directions go through <see cref="RemoteFieldKeyMap"/>, the composite-key-to-origin map
/// <see cref="RemoteSchemaAggregator.BuildContent"/> rebuilds after every poll cycle - the same
/// namespacing already used for collection loaders and action relays, applied uniformly to plain
/// fields and document-backed collections too.
/// </remarks>
internal sealed class RemoteDocumentMergeService(
    RemoteFieldKeyMap keyMap,
    RemoteInstanceStateStore stateStore,
    IRemoteInstanceClientFactory clientFactory,
    IConfigDocumentEngine engine
)
{
    private static readonly ConfigSchema RawSchema = new() { Id = "__remote-raw__" };

    /// <summary>
    /// Overlays every currently mirrored composite key's live remote value onto
    /// <paramref name="document"/>, read from each origin's last-polled document JSON.
    /// </summary>
    public void ApplyRemoteValues(ConfigDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        foreach ((string compositeKey, RemoteFieldOrigin origin) in keyMap.Current)
        {
            RemoteInstanceSnapshot? snapshot = stateStore.TryGet(origin.InstanceName);
            if (snapshot?.DocumentJson is not { } json)
            {
                continue;
            }

            ConfigDocument originDocument = engine.Parse(json, RawSchema).Document;
            document[compositeKey] = originDocument[origin.OriginalKey];
        }
    }

    /// <summary>
    /// Splits every composite key in <paramref name="mergedDocument"/> by the instance it came
    /// from, patches each changed value into that instance's last-known full document, and posts
    /// the result to that instance's own <c>POST document</c> endpoint with its own credentials -
    /// only for an instance whose mirrored fields actually changed. Never touches an instance whose
    /// mirrored fields are unchanged.
    /// </summary>
    public async Task<RemoteDocumentSaveOutcome> SplitAndSaveAsync(
        ConfigDocument mergedDocument,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(mergedDocument);

        List<RemoteDocumentSaveFailure> failures = [];

        foreach (
            IGrouping<
                string,
                KeyValuePair<string, RemoteFieldOrigin>
            > group in keyMap.Current.GroupBy(kv => kv.Value.InstanceName)
        )
        {
            string instanceName = group.Key;
            RemoteInstanceSnapshot? snapshot = stateStore.TryGet(instanceName);
            string baseline = snapshot?.DocumentJson ?? "{}";
            ConfigDocument originDocument = engine.Parse(baseline, RawSchema).Document;

            bool changed = false;
            foreach (KeyValuePair<string, RemoteFieldOrigin> entry in group)
            {
                object? incoming = mergedDocument[entry.Key];
                object? current = originDocument[entry.Value.OriginalKey];
                if (!ValuesEqual(incoming, current))
                {
                    originDocument[entry.Value.OriginalKey] = incoming;
                    changed = true;
                }
            }

            if (!changed)
            {
                continue;
            }

            string payload = engine.Serialize(originDocument);
            IRemoteInstanceClient client = clientFactory.Create(instanceName);
            RemoteDocumentSaveResult result = await client.PostDocumentAsync(
                payload,
                cancellationToken
            );

            if (!result.Success)
            {
                failures.Add(
                    new RemoteDocumentSaveFailure(
                        instanceName,
                        result.Error ?? "The instance rejected the save.",
                        result.MissingRequiredKeys,
                        result.InvalidValues
                    )
                );
            }
        }

        return failures.Count == 0
            ? RemoteDocumentSaveOutcome.Ok
            : RemoteDocumentSaveOutcome.Failed(failures);
    }

    private static bool ValuesEqual(object? a, object? b) =>
        System.Text.Json.JsonSerializer.Serialize(a)
        == System.Text.Json.JsonSerializer.Serialize(b);
}
