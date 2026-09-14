using ConfigForge.Abstractions;
using ConfigForge.AspNet.Endpoints;
using ConfigForge.AspNet.RemoteInstances;
using ConfigForge.Core.Plugins;
using ConfigForge.Core.Schema;
using Xunit;

namespace ConfigForge.AspNet.Tests;

/// <summary>
/// Two different remote instances can legitimately expose identically-labelled categories (e.g.
/// both run the same product). <see cref="RemoteSchemaAggregator"/> must namespace every mirrored
/// category's identity (<see cref="CategoryElement.CategoryKey"/>) and every mirrored action's
/// target (<see cref="ActionDefinition.CategoryKey"/>) by instance, so this collision never causes
/// one instance's actions to resolve against the other's tab.
/// </summary>
public sealed class RemoteSchemaAggregatorTests
{
    private static ManifestResponse ManifestWithUsersCategory(string actionId) =>
        new(
            SchemaId: "dash",
            Name: "Dashboard Demo",
            Version: null,
            Categories: [new ManifestCategory("Users", null, "users", "name", [])],
            Actions:
            [
                new ManifestAction(actionId, "Sync", "Users", null, false, "secondary", "top"),
            ]
        );

    private static RemoteInstanceSnapshot OnlineSnapshot(string instanceName, string actionId) =>
        RemoteInstanceSnapshot.Online(
            instanceName,
            DateTimeOffset.UtcNow,
            ManifestWithUsersCategory(actionId),
            new Dictionary<string, IReadOnlyList<CollectionEntryResponse>>(StringComparer.Ordinal)
            {
                ["users"] =
                [
                    new CollectionEntryResponse(
                        "0",
                        new Dictionary<string, object?> { ["name"] = "Ada" }
                    ),
                ],
            }
        );

    // Mirrors a real host's "General" category: a nested Categorization ("Instance"/"Hosting" tabs)
    // wrapping the actual fields, the same shape DashboardEndpoints' manifest now carries instead
    // of a flat field list.
    private static ManifestResponse ManifestWithNestedTabGroups() =>
        new(
            SchemaId: "orders-service",
            Name: "orders-service",
            Version: null,
            Categories:
            [
                new ManifestCategory(
                    "General",
                    null,
                    null,
                    null,
                    [
                        new ManifestElement(
                            "Categorization",
                            null,
                            null,
                            [
                                new ManifestElement(
                                    "Category",
                                    "Instance",
                                    null,
                                    [
                                        new ManifestElement(
                                            "Control",
                                            null,
                                            new ManifestField(
                                                "instanceName",
                                                "text",
                                                "Instance Name",
                                                false
                                            ),
                                            []
                                        ),
                                    ]
                                ),
                                new ManifestElement(
                                    "Category",
                                    "Hosting",
                                    null,
                                    [
                                        new ManifestElement(
                                            "Control",
                                            null,
                                            new ManifestField(
                                                "hosting/listenUrls",
                                                "taglist",
                                                "Listen URLs",
                                                false
                                            ),
                                            []
                                        ),
                                    ]
                                ),
                            ]
                        ),
                    ]
                ),
            ],
            Actions: []
        );

    [Fact]
    public void BuildContent_CategoryWithNestedTabGroups_ReconstructsTheSameNestingNotAFlatList()
    {
        RemoteInstanceSnapshot snapshot = RemoteInstanceSnapshot.Online(
            "orders-service",
            DateTimeOffset.UtcNow,
            ManifestWithNestedTabGroups(),
            new Dictionary<string, IReadOnlyList<CollectionEntryResponse>>(StringComparer.Ordinal)
        );

        (IReadOnlyList<CategoryElement> categories, _, _, _) = RemoteSchemaAggregator.BuildContent([
            snapshot,
        ]);

        CategoryElement general = Assert.Single(categories);
        UiElement categorization = Assert.Single(general.Elements);
        Assert.Equal("Categorization", categorization.Type);

        List<UiElement> tabs = [.. categorization.Elements];
        Assert.Equal(2, tabs.Count);

        UiElement instanceTab = tabs.Single(t => t.Label == "Instance");
        Assert.Equal("Category", instanceTab.Type);
        UiElement instanceControl = Assert.Single(instanceTab.Elements);
        Assert.Equal("Control", instanceControl.Type);
        Assert.Equal("orders-service__instanceName", JsonFormsScope.ToKey(instanceControl.Scope));

        UiElement hostingTab = tabs.Single(t => t.Label == "Hosting");
        UiElement hostingControl = Assert.Single(hostingTab.Elements);
        Assert.Equal(
            "orders-service__hosting/listenUrls",
            JsonFormsScope.ToKey(hostingControl.Scope)
        );
    }

    [Fact]
    public void BuildContent_SameLabelledCategoriesFromTwoInstances_GetDistinctCategoryKeys()
    {
        RemoteInstanceSnapshot instanceA = OnlineSnapshot("instanceA", "sync");
        RemoteInstanceSnapshot instanceB = OnlineSnapshot("instanceB", "sync");

        (IReadOnlyList<CategoryElement> categories, _, IReadOnlyList<ActionDefinition> actions, _) =
            RemoteSchemaAggregator.BuildContent([instanceA, instanceB]);

        Assert.Equal(2, categories.Count);
        Assert.All(categories, c => Assert.Equal("Users", c.Label));
        Assert.Equal(
            ["instanceA::Users", "instanceB::Users"],
            [.. categories.Select(c => c.EffectiveKey).OrderBy(k => k, StringComparer.Ordinal)]
        );
        Assert.NotEqual(categories[0].EffectiveKey, categories[1].EffectiveKey);

        Assert.Equal(2, actions.Count);
        foreach (ActionDefinition action in actions)
        {
            string instanceName = action.ActionId.Split("::")[0];
            CategoryElement ownCategory = categories.Single(c =>
                c.CollectionKey == $"{instanceName}__users"
            );
            Assert.Equal(ownCategory.CategoryKey, action.CategoryKey, StringComparer.Ordinal);
        }
    }

    [Fact]
    public async Task RegisterHandlers_TwoInstancesSameActionId_EachRelaysOnlyToItsOwnInstance()
    {
        RemoteInstanceSnapshot instanceA = OnlineSnapshot("instanceA", "sync");
        RemoteInstanceSnapshot instanceB = OnlineSnapshot("instanceB", "sync");

        var stateStore = new RemoteInstanceStateStore();
        stateStore.Update(instanceA);
        stateStore.Update(instanceB);

        var relay = new RecordingRelay();
        var registry = new PluginRegistry();

        RemoteSchemaAggregator.RegisterHandlers(
            registry,
            stateStore,
            relay,
            [instanceA, instanceB]
        );

        Assert.True(registry.TryGetAction("instanceA::sync", out var handlerA));
        Assert.True(registry.TryGetAction("instanceB::sync", out var handlerB));

        await handlerA!(new NoOpActionContext());
        await handlerB!(new NoOpActionContext());

        Assert.Equal(["instanceA", "instanceB"], relay.InvokedInstances);
    }

    [Fact]
    public void BuildContent_NeverSucceededInstance_GetsStatusPlaceholderNamedAfterInstance()
    {
        RemoteInstanceSnapshot snapshot = RemoteInstanceSnapshot.Unreachable(
            "instanceC",
            DateTimeOffset.UtcNow,
            "Connection failed: no such host is known.",
            RemoteInstanceFailureReason.ConnectionFailed,
            null,
            previous: null
        );

        (IReadOnlyList<CategoryElement> categories, _, IReadOnlyList<ActionDefinition> actions, _) =
            RemoteSchemaAggregator.BuildContent([snapshot]);

        CategoryElement placeholder = Assert.Single(categories);
        Assert.Equal("instanceC", placeholder.GroupLabel);
        Assert.Equal("Connection failed", placeholder.Label);
        Assert.Contains("no such host is known", placeholder.Description, StringComparison.Ordinal);
        Assert.Empty(actions);
    }

    [Fact]
    public void BuildContent_TimedOutInstance_GetsTimedOutPlaceholder()
    {
        RemoteInstanceSnapshot snapshot = RemoteInstanceSnapshot.Unreachable(
            "instanceC",
            DateTimeOffset.UtcNow,
            "Timed out while polling the instance.",
            RemoteInstanceFailureReason.TimedOut,
            null,
            previous: null
        );

        (IReadOnlyList<CategoryElement> categories, _, _, _) = RemoteSchemaAggregator.BuildContent([
            snapshot,
        ]);

        Assert.Equal("Timed out", Assert.Single(categories).Label);
    }

    [Fact]
    public void BuildContent_HttpErrorInstance_GetsPlaceholderWithStatusCode()
    {
        RemoteInstanceSnapshot snapshot = RemoteInstanceSnapshot.Unreachable(
            "instanceC",
            DateTimeOffset.UtcNow,
            "HTTP 500: internal error",
            RemoteInstanceFailureReason.HttpError,
            500,
            previous: null
        );

        (IReadOnlyList<CategoryElement> categories, _, _, _) = RemoteSchemaAggregator.BuildContent([
            snapshot,
        ]);

        Assert.Equal("HTTP error 500", Assert.Single(categories).Label);
    }

    [Fact]
    public void BuildContent_UnauthorizedInstance_GetsAuthenticationPlaceholder()
    {
        RemoteInstanceSnapshot snapshot = RemoteInstanceSnapshot.Unauthorized(
            "instanceC",
            DateTimeOffset.UtcNow,
            previous: null
        );

        (IReadOnlyList<CategoryElement> categories, _, _, _) = RemoteSchemaAggregator.BuildContent([
            snapshot,
        ]);

        Assert.Equal("Authentication rejected", Assert.Single(categories).Label);
    }

    [Fact]
    public void BuildContent_InstanceGoesOfflineAfterPreviousSuccess_KeepsStaleCategoriesAndAddsStatus()
    {
        RemoteInstanceSnapshot online = OnlineSnapshot("instanceA", "sync");
        RemoteInstanceSnapshot offline = RemoteInstanceSnapshot.Unreachable(
            "instanceA",
            DateTimeOffset.UtcNow,
            "Connection failed: connection refused.",
            RemoteInstanceFailureReason.ConnectionFailed,
            null,
            online
        );

        (IReadOnlyList<CategoryElement> categories, _, _, _) = RemoteSchemaAggregator.BuildContent([
            offline,
        ]);

        Assert.Equal(2, categories.Count);
        Assert.Contains(categories, c => c.Label == "Connection failed");
        Assert.Contains(categories, c => c.Label == "Users");
    }

    private sealed class RecordingRelay : IRemoteActionRelay
    {
        public List<string> InvokedInstances { get; } = [];

        public Task<RemoteActionResult> InvokeAsync(
            string instanceName,
            string actionId,
            string? entryKey,
            CancellationToken cancellationToken
        )
        {
            InvokedInstances.Add(instanceName);
            return Task.FromResult(
                RemoteActionResult.Completed(instanceName, actionId, true, null, null)
            );
        }
    }

    private sealed class NoOpActionContext : IActionContext
    {
        public string this[string fieldKey] => string.Empty;

        public Task ShowToastAsync(string message, ToastSeverity severity) => Task.CompletedTask;

        public Task SetFieldValueAsync(string fieldKey, object? value) => Task.CompletedTask;

        public Task SetFieldOptionsAsync(string fieldKey, IReadOnlyList<SelectOption> options) =>
            Task.CompletedTask;

        public Task SetFieldLoadingAsync(string fieldKey, bool loading) => Task.CompletedTask;

        public Task SetFieldEnabledAsync(string fieldKey, bool enabled) => Task.CompletedTask;

        public string CurrentFieldKey => string.Empty;

        public IServiceProvider Services => throw new NotSupportedException();

        public CancellationToken CancellationToken => CancellationToken.None;
    }
}
