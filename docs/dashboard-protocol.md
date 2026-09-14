# Headless dashboard protocol

Open mode's UI is Blazor Server: a person opens a browser and drives one host. A remote
aggregator that wants to poll many hosts and roll them into one operations dashboard can't
speak Blazor Server, and shouldn't need product-specific code to talk to each host it polls.

`UseConfigForge()` maps three plain HTTP+JSON endpoints alongside the interactive UI, under
the same `PathPrefix`. They read from and act on the same resolved schema, document, and
plugin handlers the Blazor UI uses: an aggregator gets the same categories, fields, and
actions a person would see, without loading any of the host's types.

## Registering a collection loader

A collection category (`x-cf.categories[label].collection`, see [schema.md](schema.md)) is
normally a master/detail view over a `map` field in the saved document: entries are added,
edited, and removed by the user and persisted on save.

`IPluginRegistry.RegisterCollectionLoader` swaps that for live, read-only entries computed at
request time, for example "current alerts" or "failed jobs right now" rather than something
a user edits and saves:

```csharp
registry.RegisterCollectionLoader(
    "connectors",
    async (ctx, cancellationToken) =>
    {
        var rows = await myMonitor.GetActiveConnectorsAsync(cancellationToken);
        return rows
            .Select(r => new ConfigDocument(new Dictionary<string, object?> { ["name"] = r.Name }))
            .ToList();
    }
);
```

Once a collection key has a registered loader, that category renders read-only in the
interactive UI (no add/remove/edit, and nothing is persisted from it), both there and over
the headless `data` endpoint below. `RequiresEntry` actions still work against the entries the
loader returns: the acting entry's fields resolve against the loader's live row instead of the
saved document.

## The three endpoints

All three live under the host's configured `PathPrefix` (`/config-ui` by default). When more
than one schema is loaded, pass `?schemaId=` (or `schemaId` in the POST body for the action
endpoint); with exactly one schema loaded it's inferred and the parameter can be omitted.

### `GET {prefix}/manifest`

Returns the resolved schema's shape: every category and every action, regardless of whether a
plugin currently handles that action. Nothing here indicates handler registration status,
that only surfaces as a 404 when you call the action.

```json
{
  "schemaId": "dash",
  "name": "Dashboard Demo",
  "version": null,
  "categories": [
    {
      "label": "Connectors",
      "description": null,
      "collectionKey": "connectors",
      "collectionEntryLabelKey": "name",
      "elements": [
        {
          "type": "Control",
          "label": null,
          "field": { "key": "name", "controlType": "string", "title": "Name", "required": false },
          "elements": []
        }
      ]
    },
    {
      "label": "General",
      "description": null,
      "collectionKey": null,
      "collectionEntryLabelKey": null,
      "elements": [
        {
          "type": "Categorization",
          "label": null,
          "field": null,
          "elements": [
            {
              "type": "Category",
              "label": "Instance",
              "field": null,
              "elements": [
                {
                  "type": "Control",
                  "label": null,
                  "field": { "key": "instanceName", "controlType": "text", "title": "Instance Name", "required": false },
                  "elements": []
                }
              ]
            },
            {
              "type": "Category",
              "label": "Hosting",
              "field": null,
              "elements": [
                {
                  "type": "Control",
                  "label": null,
                  "field": { "key": "hosting/listenUrls", "controlType": "taglist", "title": "Listen URLs", "required": false },
                  "elements": []
                }
              ]
            }
          ]
        }
      ]
    }
  ],
  "actions": [
    {
      "actionId": "connector.test",
      "label": "Test connection",
      "category": "Connectors",
      "section": null,
      "requiresEntry": true,
      "variant": "Default",
      "position": "Top"
    },
    {
      "actionId": "global.ping",
      "label": "Ping",
      "category": null,
      "section": null,
      "requiresEntry": false,
      "variant": "Default",
      "position": "Top"
    }
  ]
}
```

For a collection category, `elements` is a flat list of `Control` nodes describing one entry
(the map's value schema, one node per field); an entry has no further layout to mirror, and
`collectionKey`/`collectionEntryLabelKey` name the map field and the sub-key used to label an
entry.

For an ordinary category, `elements` is the real layout tree the interactive UI renders from,
not a flattened field list: each node's `type` matches a `ConfigForge.Core.Schema.UiElement.Type`
(`Control`, `Group`, `Categorization`, `Category`, `VerticalLayout`, `HorizontalLayout`, `Label`).
A `Control` node carries the field it addresses in `field` and has no `elements`; every other
type carries no `field`, an optional `label` (a group/tab title, or label text), and nests
further elements, recursively, to whatever depth the schema's layout actually has. The
"General" example above mirrors a real host's "Instance"/"Hosting" tabs (`[CfCategory("Instance")]`
/ `[CfCategory("Hosting")]`, see [schema.md](schema.md)): before this shape existed, the manifest
flattened both tabs into one undifferentiated field list, so a remote consumer had no way to
reconstruct the grouping that host's own UI shows.

### `GET {prefix}/data/{sectionId}`

Returns the entries of one collection category, `sectionId` being its `collectionKey`. A
document-backed collection returns the saved map's entries keyed by their real map key; a
loader-backed one (registered via `RegisterCollectionLoader`) calls the loader fresh for the
request and returns its rows keyed by a synthetic index (`"0"`, `"1"`, …) instead, nothing
here is read from or written to the saved document.

```json
[
  { "key": "11111111-1111-1111-1111-111111111111", "value": { "name": "Doc Row" } }
]
```

`404` if `sectionId` doesn't name a collection category in the resolved schema.

### `POST {prefix}/action/{actionId}`

Dispatches the same action handler a button press would, through the same `IActionContext`
plugins already implement (`RegisterAction`), see [plugins.md](plugins.md). The handler runs
against a document loaded fresh for the request (via `OnLoad`, secrets redacted the same way
the editor sees them); nothing about the interactive UI (dirty tracking, toasts shown on
screen) is involved.

Request body:

```json
{ "entryKey": "0", "schemaId": "dash" }
```

`entryKey` is required when the action's manifest entry has `requiresEntry: true`. It's the
`key` from the `data` endpoint's response for that action's category (a map key for a
document-backed collection, a synthetic index for a loader-backed one). `schemaId` is only
needed when more than one schema is hosted; it overrides the `?schemaId=` query string.

Response:

```json
{ "success": true, "message": "done", "severity": "Success" }
```

`message`/`severity` mirror the last toast the handler raised via `ctx.ShowToastAsync`, if
any. `success` is `false` only when that last toast's severity was `Warning` or `Danger`; no
toast, or `Info`/`Success`, both count as success.

Errors: `404` if `actionId` has no registered handler, or if the resolved `schemaId` doesn't
exist; `400` if `requiresEntry` is true and no `entryKey` was supplied.

## Full document parity: `GET`/`POST {prefix}/document`

The three endpoints above cover collection categories and actions. Everything Config Engine's
schema/document model can express, every plain field, every category, every collection, every
validation rule, every secret field, round-trips through this one endpoint pair, because it calls
exactly what the interactive Blazor editor calls: `AspNetConfigForgeOptions.OnLoad` for the raw
document, `IConfigDocumentEngine.Validate` for the same validation the save button runs, and
`AspNetConfigForgeOptions.OnSave` for the same persistence call. There is no parallel
reimplementation of load/validate/save for the headless path; a caller of this endpoint pair gets
identical behavior to a person editing the same schema in the browser, including any custom
validation a plugin registers and any exception a host's own `OnSave` throws.

The one deliberate exception is a custom compiled Blazor widget registered via
`IPluginRegistry.RegisterControl`. That's a real UI component, not data, so it cannot be expressed
as JSON. That is out of scope for this protocol entirely; see the separate
interface-capability-loader mechanism (`ICapabilityLoader`/`[CapabilityContract]`,
[capability-loading.md](capability-loading.md)) for exposing behavior like that to a remote caller.

### `GET {prefix}/document`

Calls the resolved schema's `OnLoad`, redacts secrets exactly as the editor's initial load does
(see [Secrets](aspnet.md#secrets)), and returns the raw document JSON as-is, the same shape
`OnSave` will receive back. `404` if the schema has no `OnLoad` configured, or if `schemaId`
doesn't resolve (same resolution rules as `manifest`/`data`/`action`).

### `POST {prefix}/document`

Posts a full document JSON body. Runs it through the same pipeline the interactive save button
does:

1. Parse (`IConfigDocumentEngine.Parse`); malformed JSON returns `400`:
   ```json
   { "error": "The posted body is not valid JSON.", "jsonError": "...", "missingRequiredKeys": [], "invalidValues": [] }
   ```
2. Validate (`IConfigDocumentEngine.Validate`); a required field missing or a value failing a
   schema constraint returns `422` with the same structured shape, `missingRequiredKeys` and/or
   `invalidValues` populated instead of empty:
   ```json
   { "error": "The document failed validation.", "jsonError": null, "missingRequiredKeys": ["name"], "invalidValues": [] }
   ```
3. Resolve secrets (`ConfigSecretGateway.MergeForStore`) against the currently stored document.
   `ConfigForgeSecret.StoredMarker` keeps the stored value, a new value is encrypted, empty clears
   it, exactly as a human save does.
4. Call `OnSave`. A host's own `OnSave` throwing (e.g. a domain rule like "instance names must be
   unique") is caught and reported as `400` with the exception message in `error`, rather than a
   bare `500`.

On success: `200 { "success": true }`. `404` if the schema has no `OnSave` configured.

## Consuming another host's dashboard: `RemoteInstances`

The endpoints above exist so *something* can poll a host headlessly. `ConfigForge.AspNet`
ships one built-in consumer of its own protocol: `AspNetConfigForgeOptions.RemoteInstances`. Any
host using `AddConfigForge`/`UseConfigForge` can list other ConfigForge-hosted instances, and every
reachable category, collection or plain field, is mirrored into this host's own schema and
document automatically, with real read/write parity, not just a read-only subset.

```csharp
builder.AddConfigForge(options =>
{
    options.OnLoad = ...; // this host's own local schema/document, unrelated to the below
    options.OnSave = ...;
    options.RemoteInstances =
    [
        new RemoteInstanceOptions
        {
            Name = "orders-service",
            BaseUrl = new Uri("https://orders.internal.example.com"),
            PathPrefix = "/config-ui",
            Username = "aggregator",
            Password = "...",
        },
    ];
});
```

A background service polls every configured instance's `/manifest`, `/data/{sectionId}` (for each
loader-backed collection), and `/document` on an interval (`RemoteInstancePollIntervalSeconds`,
default 30) using that instance's own credentials, and merges the result into every schema this
host has registered via `IConfigForgeHostState`:

- **Composes, never replaces.** A mirrored category is appended alongside whatever categories the
  host's own `OnLoad`/local schema already contributes. Remote categories are added ahead of the
  host's own, so a host whose only local content is one always-relevant category (e.g. its own
  "Manage Instances" list) keeps that category last regardless of how many instances are
  connected. A host with a real local schema of its own (its own categories, fields, actions) can
  enable `RemoteInstances` too; nothing about the feature is specific to being empty locally.
- **Grouped by instance.** Each instance's categories render under a sidebar heading naming that
  instance (its manifest's `name`, or its configured `Name`) via `CategoryElement.GroupLabel`.
- **Action buttons relay to the originating instance.** Every mirrored action's id is namespaced
  (`{instanceName}::{actionId}`) and its handler, registered automatically, calls back to that
  instance's `POST /action/{actionId}` with that instance's own credentials, never any other
  connected instance's.

### Every category kind mirrors, not just collections

Superseding the earlier version of this protocol (where only collection categories mirrored), the
manifest's `categories[].collectionIsLoaderBacked` flag (null for a plain category; `true`/`false`
for a collection, reflecting whether the origin registered a collection loader for it) tells the
aggregator how to treat each category:

- **A plain field category** (no `collectionKey`) mirrors every field under a namespaced key
  (`{instanceName}__{fieldKey}`, preserving any `/` nesting in `fieldKey`, for example
  `orders-service__hosting/listenUrls`) as a real, editable field in the merged schema. Its value comes from
  the origin's last-polled `GET /document`, not an empty placeholder. The manifest's `elements` tree
  (groups, tab groups, layouts) is reconstructed as-is around the namespaced fields, not discarded:
  a mirrored "General" category shows the origin's own "Instance"/"Hosting" tabs, not one flat page.
- **A loader-backed collection** (`collectionIsLoaderBacked: true`, live/computed rows, like
  a live status/alerts/failed-item collection) keeps the existing read-only-with-refresh treatment:
  a collection loader is registered locally that calls the cached `GET /data/{sectionId}` rows.
  Nothing here changed.
- **A document-backed collection** (`collectionIsLoaderBacked: false`, an ordinary, user-editable,
  document-backed map on the origin) mirrors as a real editable map field instead: no collection
  loader is registered, so the Hub's add/edit/remove UI applies to it exactly like any local
  document-backed collection. Its entries are populated from the origin's document, keyed by the
  origin's own real map keys (not a synthetic index).

`RemoteFieldKeyMap` records, for every composite key currently mirrored, which instance and which
original (un-namespaced) key it came from. `RemoteDocumentMergeService` uses it in both directions:

- **On load** (`GET`/`POST {prefix}/document`'s document-loading step, and the interactive editor's
  initial load), it overlays each composite key's live value, read from that origin's last-polled
  document JSON.
- **On save**, it groups the merged document's composite keys by origin, patches each *changed*
  value into that origin's last-known full document (leaving everything else about that origin's
  document untouched), and only for an origin whose mirrored fields actually differ from what was
  last polled, posts the patched document to that origin's own `POST /document` with that origin's
  own credentials. An origin whose mirrored fields are unchanged is never contacted on save.

A save is still routed through the local host's own `OnSave` afterward (for whatever local content
that host itself owns, e.g. the Hub's "Manage Instances" list); remote-derived keys reaching a
local `OnSave` that does not recognize them are simply ignored, the same as any other unknown key.

### Validation errors from an origin surface structurally, not as a generic failure

If an origin's `POST /document` rejects the patched value it was sent, whether a required field, a
schema constraint like `pattern`, or a domain rule the origin's own `OnSave` throws for, the
aggregator's own `POST /document` call also fails, with `422`/`400` and a
`DocumentSaveErrorResponse` whose `error` names the rejecting instance (e.g. `"Instance
'orders-service' rejected the save: ..."`) and carries that instance's own
`missingRequiredKeys`/`invalidValues`. Nothing here is swallowed into a bare success/failure
boolean.

### Secrets round-trip safely with no extra code

A mirrored secret field needs no special handling in the merge/split logic above, because both
directions only ever copy *opaque* values between two document endpoints that already apply the
write-only secret convention themselves:

- **On load**, the origin's own `GET /document` already redacted its secret fields to
  `ConfigForgeSecret.StoredMarker` before the aggregator ever polls it. The merge step copies that
  marker verbatim into the merged document, so the Hub's editor shows "stored", never a plaintext
  or ciphertext value.
- **On save**, an untouched secret field's composite value is still the same `StoredMarker` it was
  loaded as, so the unchanged-value check finds no difference from the origin's own last-known
  document (which also holds the marker at that path) and never even includes it in the patch sent
  to the origin. A changed secret's new plaintext *does* differ, gets patched into the origin's
  document at its real key, and is handled by the origin's own `ConfigSecretGateway.MergeForStore`
  exactly as a direct save to that origin would encrypt it. The aggregator itself never encrypts,
  decrypts, or inspects a secret value.

### Category-label collisions are namespaced, not just the action id

Two different connected instances can legitimately expose an identically-labelled category (e.g.
both run the same product). If action-button resolution matched by category *label* alone, a
label collision could show one instance's action buttons on the wrong instance's tab, even though
the underlying relay call would still (correctly) reach the right instance by its namespaced
action id. That is a display bug, but a real one, in exactly the scenario this feature exists for.

This is why `CategoryElement` carries a `CategoryKey` distinct from its display `Label`, and
`ActionDefinition` carries a matching `CategoryKey`: a mirrored category's `CategoryKey` is
namespaced by instance (`{instanceName}::{label}`), and action-button resolution
(`ActionButtonBar`) matches an action to the active category by this identity
(`CategoryElement.EffectiveKey`, which falls back to `Label` when `CategoryKey` is unset, so an
ordinary JSON-declared category, never assembled from multiple sources, behaves exactly as before
this existed). Two instances with a colliding "Users" category each show, and each dispatch,
only their own actions.

### An unreachable instance stays visible, with why

A poll can fail three distinguishable ways, each reflected in the merged sidebar rather than
silently dropping the instance:

- **Connection failure** (DNS failure, connection refused, TLS failure, …): anything the
  underlying `HttpRequestException` reports.
- **Timeout**: the poll didn't complete within the configured HTTP timeout.
- **HTTP error status**: the instance responded, but not successfully (e.g. `500`); the
  status code is preserved.
- **Authentication rejected**: a `401`/`403` response, already distinguished from the above as
  its own status.

When an instance's last poll attempt failed, its group heading still appears in the merged
sidebar, even for an instance that has never once polled successfully, which previously
contributed zero categories and vanished from the merged view entirely. A synthetic placeholder
category is added under that instance's heading showing the failure kind (and the HTTP status
code, when the failure was an HTTP error). If the instance had previously polled successfully,
its last-known categories keep rendering alongside the placeholder (the existing graceful
degradation), so the sidebar reflects "was working, now failing" rather than swapping the whole
group out. The placeholder disappears again on the next poll that succeeds.

### Instance names must be unique

`RemoteInstanceOptions.Name` doubles as the namespace for every mirrored category and action key
(`{instanceName}::...`), so two configured instances sharing the same `Name` would silently
collide and break the per-instance isolation described above. The Hub's "Manage Instances" save
path rejects such a save (case-insensitively, `"orders-service"` and `"Orders-Service"` collide)
with a clear validation error surfaced through the normal save-error toast, not a crash. Two
different names pointing at the same `BaseUrl` (e.g. staging and prod of the same product) are
unaffected: only a duplicate *name* is rejected, never a duplicate underlying instance.

## Auth

ConfigForge has no built-in authentication or authorization on these endpoints, the same as
it has none for the interactive UI. `UseConfigForge()` calls `UseRouting()` before mapping
its routes, so any auth middleware the host added earlier in the pipeline (`UseAuthentication()`,
`UseAuthorization()`, a custom Basic Auth handler, …) applies to `/manifest`, `/data/{id}`, and
`/action/{id}` exactly as it applies to everything else the host serves. Add it before calling
`UseConfigForge()`, the normal ASP.NET Core middleware-ordering rule. If the host adds no auth,
these endpoints are open, same as the browser UI would be.
