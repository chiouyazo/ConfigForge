namespace ConfigForge.AspNet.Endpoints;

/// <summary>The request body of the action endpoint.</summary>
/// <param name="EntryKey">
/// The acting entry's key. Required when the action's manifest entry has <c>RequiresEntry</c>.
/// </param>
/// <param name="SchemaId">
/// The schema to act against, when more than one schema is hosted. Overrides the query string.
/// </param>
/// <param name="Args">
/// Present only for a capability-widget proxy call (<c>docs/capability-loading.md</c>): the
/// method's arguments, in declaration order, with any trailing <c>CancellationToken</c> already
/// stripped by the caller.
/// </param>
public sealed record ActionRequest(
    string? EntryKey,
    string? SchemaId,
    IReadOnlyList<object?>? Args = null
);
