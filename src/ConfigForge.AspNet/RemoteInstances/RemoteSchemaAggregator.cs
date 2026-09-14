using ConfigForge.Abstractions;
using ConfigForge.AspNet.Endpoints;
using ConfigForge.Core.Plugins;
using ConfigForge.Core.Schema;

namespace ConfigForge.AspNet.RemoteInstances;

/// <summary>
/// Projects polled <see cref="RemoteInstanceSnapshot"/>s into the categories/fields/actions a host
/// schema merges in via <see cref="IConfigForgeHostState.SetRemoteContent"/>: every reachable
/// instance's categories - collection or plain - appear grouped under a
/// <see cref="CategoryElement.GroupLabel"/> naming that instance. Also registers the collection
/// loaders and action relays the rendered categories need, and reports the composite-key-to-origin
/// map <see cref="RemoteDocumentMergeService"/> needs to overlay and split real document values.
/// </summary>
/// <remarks>
/// Two different instances may legitimately expose identically-labelled categories (e.g. both run
/// the same product). A category's <see cref="CategoryElement.Label"/> is display-only here: its
/// <see cref="CategoryElement.CategoryKey"/> - and every <see cref="ActionDefinition.CategoryKey"/>
/// placed in it - is namespaced by <see cref="RemoteInstanceSnapshot.InstanceName"/>
/// (<see cref="CompositeCategoryKey"/>), so action-button resolution (which keys off
/// <see cref="CategoryElement.EffectiveKey"/>, see <c>ActionButtonBar.razor</c>) can never confuse
/// one instance's actions for another's just because their category labels collide.
/// </remarks>
internal static class RemoteSchemaAggregator
{
    public static (
        IReadOnlyList<CategoryElement> Categories,
        IReadOnlyDictionary<string, FieldDefinition> Fields,
        IReadOnlyList<ActionDefinition> Actions,
        IReadOnlyDictionary<string, RemoteFieldOrigin> FieldOrigins
    ) BuildContent(
        IReadOnlyList<RemoteInstanceSnapshot> snapshots,
        ICapabilityWidgetCatalog? capabilityWidgets = null
    )
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        List<CategoryElement> categories = [];
        Dictionary<string, FieldDefinition> fields = new(StringComparer.Ordinal);
        List<ActionDefinition> actions = [];
        Dictionary<string, RemoteFieldOrigin> fieldOrigins = new(StringComparer.Ordinal);

        foreach (
            RemoteInstanceSnapshot snapshot in snapshots.OrderBy(
                s => s.InstanceName,
                StringComparer.Ordinal
            )
        )
        {
            string groupLabel = string.IsNullOrEmpty(snapshot.Manifest?.Name)
                ? snapshot.InstanceName
                : snapshot.Manifest.Name;

            if (snapshot.Status != RemoteInstanceStatus.Online)
            {
                categories.Add(BuildStatusPlaceholderCategory(snapshot, groupLabel));
            }

            if (snapshot.Manifest is null)
            {
                continue;
            }

            // Category label -> its namespaced key, so the action loop below (which walks every
            // manifest action once, not per-category) can resolve which composite category an
            // action belongs to without re-deriving the label -> key mapping per action.
            Dictionary<string, string> categoryKeyByLabel = new(StringComparer.Ordinal);

            foreach (ManifestCategory category in snapshot.Manifest.Categories)
            {
                string categoryKey = CompositeCategoryKey(snapshot.InstanceName, category.Label);
                categoryKeyByLabel[category.Label] = categoryKey;

                if (category.CollectionKey is not { Length: > 0 } collectionKey)
                {
                    categories.Add(
                        BuildPlainCategory(
                            snapshot.InstanceName,
                            category,
                            groupLabel,
                            categoryKey,
                            fields,
                            fieldOrigins,
                            capabilityWidgets
                        )
                    );
                    continue;
                }

                bool loaderBacked = category.CollectionIsLoaderBacked ?? true;
                string compositeCollectionKey = CompositeCollectionKey(
                    snapshot.InstanceName,
                    collectionKey
                );

                fields[compositeCollectionKey] = new FieldDefinition
                {
                    Key = compositeCollectionKey,
                    ControlType = "map",
                    ValueField = new FieldDefinition
                    {
                        ControlType = "object",
                        Children =
                        [
                            .. category
                                .Elements.Where(e => e.Field is not null)
                                .Select(e => new FieldDefinition
                                {
                                    Key = e.Field!.Key,
                                    ControlType = e.Field!.ControlType,
                                    Title = e.Field!.Title,
                                    Required = e.Field!.Required,
                                    ReadOnly = loaderBacked || e.Field!.ReadOnly,
                                }),
                        ],
                    },
                };

                if (!loaderBacked)
                {
                    fieldOrigins[compositeCollectionKey] = new RemoteFieldOrigin(
                        snapshot.InstanceName,
                        collectionKey
                    );
                }

                categories.Add(
                    new CategoryElement
                    {
                        Label = category.Label,
                        Description = category.Description,
                        GroupLabel = groupLabel,
                        CategoryKey = categoryKey,
                        CollectionKey = compositeCollectionKey,
                        CollectionEntryLabelKey = category.CollectionEntryLabelKey,
                    }
                );
            }

            foreach (ManifestAction action in snapshot.Manifest.Actions)
            {
                string? categoryKey =
                    action.Category is { Length: > 0 } label
                    && categoryKeyByLabel.TryGetValue(label, out string? key)
                        ? key
                        : null;

                actions.Add(
                    new ActionDefinition
                    {
                        ActionId = CompositeActionId(snapshot.InstanceName, action.ActionId),
                        Label = action.Label,
                        Category = action.Category,
                        CategoryKey = categoryKey,
                        RequiresEntry = action.RequiresEntry,
                        Variant = action.Variant,
                        Position = action.Position,
                    }
                );
            }
        }

        return (categories, fields, actions, fieldOrigins);
    }

    private static CategoryElement BuildPlainCategory(
        string instanceName,
        ManifestCategory category,
        string groupLabel,
        string categoryKey,
        Dictionary<string, FieldDefinition> fields,
        Dictionary<string, RemoteFieldOrigin> fieldOrigins,
        ICapabilityWidgetCatalog? capabilityWidgets
    )
    {
        List<UiElement> elements =
        [
            .. category.Elements.Select(e =>
                ToUiElement(instanceName, e, fields, fieldOrigins, capabilityWidgets)
            ),
        ];

        return new CategoryElement
        {
            Label = category.Label,
            Description = category.Description,
            GroupLabel = groupLabel,
            CategoryKey = categoryKey,
            Elements = elements,
        };
    }

    /// <summary>
    /// Converts one node of a mirrored category's <see cref="ManifestElement"/> tree into the
    /// equivalent <see cref="UiElement"/>, recursively, so the local rendering the interactive UI
    /// gives every other category (grouped fields, tabs, layouts) also applies to a mirrored one -
    /// rather than always flattening it to a bare list of controls. A <c>Control</c> leaf's field is
    /// registered under its instance-namespaced composite key, with its origin recorded for
    /// <see cref="RemoteDocumentMergeService"/> to resolve real values against.
    /// </summary>
    private static UiElement ToUiElement(
        string instanceName,
        ManifestElement element,
        Dictionary<string, FieldDefinition> fields,
        Dictionary<string, RemoteFieldOrigin> fieldOrigins,
        ICapabilityWidgetCatalog? capabilityWidgets
    )
    {
        if (
            string.Equals(element.Type, "Control", StringComparison.Ordinal)
            && element.Field is { } field
        )
        {
            string compositeFieldKey = CompositeFieldKey(instanceName, field.Key);
            string controlType = ResolveControlType(
                instanceName,
                field.ControlType,
                capabilityWidgets
            );
            fields[compositeFieldKey] = new FieldDefinition
            {
                Key = compositeFieldKey,
                ControlType = controlType,
                Title = field.Title,
                Required = field.Required,
                ReadOnly = field.ReadOnly,
            };
            fieldOrigins[compositeFieldKey] = new RemoteFieldOrigin(instanceName, field.Key);
            return new UiElement
            {
                Type = "Control",
                Scope = JsonFormsScope.ToScope(compositeFieldKey),
            };
        }

        return new UiElement
        {
            Type = element.Type,
            Label = element.Label,
            Elements =
            [
                .. element.Elements.Select(child =>
                    ToUiElement(instanceName, child, fields, fieldOrigins, capabilityWidgets)
                ),
            ],
        };
    }

    /// <summary>
    /// A mirrored field whose control type names a widget the origin's own capability assembly
    /// registered for this instance (<see cref="ICapabilityWidgetCatalog.TryGetWidget"/>) is
    /// rewritten to its composite control type, so <c>FieldRenderer</c>'s later lookup by the
    /// same composite key resolves it to that instance's own widget and proxy, never another
    /// connected instance's. An ordinary control type (no matching widget registered) is left
    /// untouched, exactly as before this existed.
    /// </summary>
    private static string ResolveControlType(
        string instanceName,
        string controlType,
        ICapabilityWidgetCatalog? capabilityWidgets
    )
    {
        if (capabilityWidgets is null)
        {
            return controlType;
        }

        string composite = CompositeControlType(instanceName, controlType);
        return capabilityWidgets.TryGetWidget(composite, out _) ? composite : controlType;
    }

    /// <summary>
    /// Registers the collection loaders and action relays every currently known instance's
    /// mirrored categories need. Safe to call repeatedly (registrations upsert), so it is run again
    /// after every poll cycle to pick up instances that appeared or changed shape. Only a
    /// loader-backed remote collection gets a collection loader registered here: a document-backed
    /// one is instead populated by <see cref="RemoteDocumentMergeService"/> as real document values,
    /// so it can support real add/edit/remove through the merged document like any other.
    /// </summary>
    public static void RegisterHandlers(
        IPluginRegistry pluginRegistry,
        RemoteInstanceStateStore stateStore,
        IRemoteActionRelay actionRelay,
        IReadOnlyList<RemoteInstanceSnapshot> snapshots
    )
    {
        ArgumentNullException.ThrowIfNull(pluginRegistry);
        ArgumentNullException.ThrowIfNull(stateStore);
        ArgumentNullException.ThrowIfNull(actionRelay);
        ArgumentNullException.ThrowIfNull(snapshots);

        foreach (RemoteInstanceSnapshot snapshot in snapshots)
        {
            if (snapshot.Manifest is null)
            {
                continue;
            }

            string instanceName = snapshot.InstanceName;
            foreach (ManifestCategory category in snapshot.Manifest.Categories)
            {
                bool loaderBacked = category.CollectionIsLoaderBacked ?? true;
                string? compositeCollectionKey = category.CollectionKey
                    is { Length: > 0 } collectionKey
                    ? CompositeCollectionKey(instanceName, collectionKey)
                    : null;

                if (compositeCollectionKey is not null && loaderBacked)
                {
                    pluginRegistry.RegisterCollectionLoader(
                        compositeCollectionKey,
                        (_, _) =>
                            Task.FromResult(
                                LoadEntries(stateStore, instanceName, category.CollectionKey!)
                            )
                    );
                }

                foreach (
                    string actionId in snapshot
                        .Manifest.Actions.Where(a =>
                            string.Equals(a.Category, category.Label, StringComparison.Ordinal)
                        )
                        .Select(a => a.ActionId)
                )
                {
                    string compositeActionId = CompositeActionId(instanceName, actionId);
                    pluginRegistry.RegisterAction(
                        compositeActionId,
                        ctx =>
                            RelayActionAsync(
                                actionRelay,
                                stateStore,
                                instanceName,
                                category.CollectionKey,
                                compositeCollectionKey,
                                loaderBacked,
                                actionId,
                                ctx
                            )
                    );
                }
            }

            foreach (
                string actionId in snapshot
                    .Manifest.Actions.Where(a => a.Category is null)
                    .Select(a => a.ActionId)
            )
            {
                string compositeActionId = CompositeActionId(instanceName, actionId);
                pluginRegistry.RegisterAction(
                    compositeActionId,
                    ctx =>
                        RelayActionAsync(
                            actionRelay,
                            stateStore,
                            instanceName,
                            null,
                            null,
                            true,
                            actionId,
                            ctx
                        )
                );
            }
        }
    }

    private const string StatusPlaceholderLabel = "__status__";

    private static CategoryElement BuildStatusPlaceholderCategory(
        RemoteInstanceSnapshot snapshot,
        string groupLabel
    ) =>
        new()
        {
            Label = DescribeStatus(snapshot),
            Description = snapshot.ErrorMessage,
            GroupLabel = groupLabel,
            CategoryKey = CompositeCategoryKey(snapshot.InstanceName, StatusPlaceholderLabel),
        };

    private static string DescribeStatus(RemoteInstanceSnapshot snapshot) =>
        snapshot.Status switch
        {
            RemoteInstanceStatus.Unauthorized => "Authentication rejected",
            RemoteInstanceStatus.Offline => snapshot.FailureReason switch
            {
                RemoteInstanceFailureReason.TimedOut => "Timed out",
                RemoteInstanceFailureReason.HttpError => snapshot.HttpStatusCode is { } code
                    ? $"HTTP error {code}"
                    : "HTTP error",
                RemoteInstanceFailureReason.ConnectionFailed => "Connection failed",
                _ => "Unreachable",
            },
            _ => "Unavailable",
        };

    public static string CompositeCollectionKey(string instanceName, string collectionKey) =>
        $"{instanceName}__{collectionKey}";

    public static string CompositeFieldKey(string instanceName, string originalKey)
    {
        int slash = originalKey.IndexOf('/', StringComparison.Ordinal);
        return slash < 0
            ? $"{instanceName}__{originalKey}"
            : $"{instanceName}__{originalKey[..slash]}{originalKey[slash..]}";
    }

    public static string CompositeActionId(string instanceName, string actionId) =>
        $"{instanceName}::{actionId}";

    public static string CompositeCategoryKey(string instanceName, string categoryLabel) =>
        $"{instanceName}::{categoryLabel}";

    /// <summary>
    /// The key a capability widget is registered and looked up under: namespaced by instance so
    /// two connected instances of the same product, each with their own widget id, can never be
    /// confused for one another even though their origin declared the same plain control type.
    /// </summary>
    public static string CompositeControlType(string instanceName, string controlType) =>
        $"{instanceName}::{controlType}";

    private static IReadOnlyList<ConfigDocument> LoadEntries(
        RemoteInstanceStateStore stateStore,
        string instanceName,
        string collectionKey
    )
    {
        RemoteInstanceSnapshot? snapshot = stateStore.TryGet(instanceName);
        if (
            snapshot is null
            || !snapshot.SectionData.TryGetValue(
                collectionKey,
                out IReadOnlyList<CollectionEntryResponse>? entries
            )
        )
        {
            return [];
        }

        return [.. entries.Select(e => new ConfigDocument(Flatten(e.Value)))];
    }

    private static async Task RelayActionAsync(
        IRemoteActionRelay actionRelay,
        RemoteInstanceStateStore stateStore,
        string instanceName,
        string? collectionKey,
        string? compositeCollectionKey,
        bool loaderBacked,
        string actionId,
        IActionContext ctx
    )
    {
        string? entryKey = null;
        if (collectionKey is { Length: > 0 } && compositeCollectionKey is { Length: > 0 })
        {
            entryKey = loaderBacked
                ? ResolveLoaderEntryKey(
                    stateStore,
                    instanceName,
                    collectionKey,
                    compositeCollectionKey,
                    ctx.CurrentFieldKey
                )
                : ResolveDocumentEntryKey(compositeCollectionKey, ctx.CurrentFieldKey);
        }

        RemoteActionResult result = await actionRelay.InvokeAsync(
            instanceName,
            actionId,
            entryKey,
            ctx.CancellationToken
        );

        if (result.Message is { Length: > 0 } message)
        {
            await ctx.ShowToastAsync(
                message,
                result.Success ? ToastSeverity.Success : ToastSeverity.Danger
            );
        }
    }

    /// <summary>
    /// For a document-backed mirrored collection, the merged document's entry key under the
    /// composite map field is already the origin's own real map key (populated verbatim by
    /// <see cref="RemoteDocumentMergeService"/>), so no index translation is needed - unlike a
    /// loader-backed collection's synthetic per-request index.
    /// </summary>
    private static string? ResolveDocumentEntryKey(
        string compositeCollectionKey,
        string currentFieldKey
    )
    {
        string prefix = compositeCollectionKey + "/";
        if (!currentFieldKey.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        string remainder = currentFieldKey[prefix.Length..];
        int separatorIndex = remainder.IndexOf('/', StringComparison.Ordinal);
        return separatorIndex < 0 ? remainder : remainder[..separatorIndex];
    }

    private static string? ResolveLoaderEntryKey(
        RemoteInstanceStateStore stateStore,
        string instanceName,
        string collectionKey,
        string compositeCollectionKey,
        string currentFieldKey
    )
    {
        string prefix = compositeCollectionKey + "/";
        if (!currentFieldKey.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        string indexSegment = currentFieldKey[prefix.Length..];
        if (!int.TryParse(indexSegment, out int index) || index < 0)
        {
            return null;
        }

        RemoteInstanceSnapshot? snapshot = stateStore.TryGet(instanceName);
        if (
            snapshot is null
            || !snapshot.SectionData.TryGetValue(
                collectionKey,
                out IReadOnlyList<CollectionEntryResponse>? entries
            )
            || index >= entries.Count
        )
        {
            return null;
        }

        return entries[index].Key;
    }

    private static Dictionary<string, object?> Flatten(object? value)
    {
        Dictionary<string, object?> result = new(StringComparer.Ordinal);
        if (
            value is System.Text.Json.JsonElement
            {
                ValueKind: System.Text.Json.JsonValueKind.Object
            } obj
        )
        {
            foreach (System.Text.Json.JsonProperty property in obj.EnumerateObject())
            {
                result[property.Name] = property.Value.ValueKind switch
                {
                    System.Text.Json.JsonValueKind.String => property.Value.GetString(),
                    System.Text.Json.JsonValueKind.True => true,
                    System.Text.Json.JsonValueKind.False => false,
                    System.Text.Json.JsonValueKind.Number => property.Value.ToString(),
                    _ => property.Value.ToString(),
                };
            }
        }

        return result;
    }
}
