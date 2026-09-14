using System.Globalization;
using ConfigForge.Abstractions;
using ConfigForge.AspNet.RemoteInstances;
using ConfigForge.Blazor.Services;
using ConfigForge.Core.Documents;
using ConfigForge.Core.Plugins;
using ConfigForge.Core.Schema;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ConfigForge.AspNet.Endpoints;

/// <summary>
/// Headless HTTP endpoints exposing the same resolved schema, document, and action
/// dispatch that the interactive Blazor UI renders from, so a remote caller can
/// consume a ConfigForge host without loading any product-specific code: it only
/// ever speaks HTTP+JSON.
/// </summary>
internal static class DashboardEndpoints
{
    /// <summary>Maps the manifest, data, and action endpoints under the given prefix.</summary>
    /// <param name="endpoints">The endpoint route builder to map onto.</param>
    /// <param name="prefix">The configured <see cref="AspNetConfigForgeOptions.PathPrefix"/>.</param>
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder endpoints, string prefix)
    {
        RouteGroupBuilder group = endpoints.MapGroup(prefix);

        group.MapGet("/manifest", GetManifest);
        group.MapGet("/data/{sectionId}", GetDataAsync);
        group.MapPost("/action/{actionId}", PostActionAsync);
        group.MapGet("/document", GetDocumentAsync);
        group.MapPost("/document", PostDocumentAsync);
    }

    private static IResult GetManifest(
        IConfigForgeHostState state,
        IPluginCatalog catalog,
        [FromQuery] string? schemaId
    )
    {
        (ConfigSchema? schema, IResult? error) = ResolveSchema(state, schemaId);
        if (schema is null)
        {
            return error!;
        }

        ManifestCategory[] categories =
        [
            .. schema.Categories.Select(c => BuildCategory(c, schema, catalog)),
        ];
        ManifestAction[] actions = [.. schema.Actions.Select(BuildAction)];

        return Results.Ok(
            new ManifestResponse(schema.Id, schema.Name, schema.Version, categories, actions)
        );
    }

    private static async Task<IResult> GetDocumentAsync(
        [FromQuery] string? schemaId,
        IConfigForgeHostState state,
        ConfigSecretGateway secrets,
        IConfigDocumentEngine engine,
        IServiceProvider services
    )
    {
        (ConfigSchema? schema, IResult? error) = ResolveSchema(state, schemaId);
        if (schema is null)
        {
            return error!;
        }

        if (state.Options.OnLoad is null)
        {
            return Results.NotFound(
                new { error = $"Schema '{schema.Id}' has no document persistence configured." }
            );
        }

        ConfigDocument document = await LoadDocumentAsync(
            schema,
            state.Options,
            secrets,
            engine,
            services
        );
        return Results.Text(engine.Serialize(document, schema), "application/json");
    }

    private static async Task<IResult> PostDocumentAsync(
        [FromQuery] string? schemaId,
        IConfigForgeHostState state,
        ConfigSecretGateway secrets,
        IConfigDocumentEngine engine,
        IServiceProvider services,
        HttpContext httpContext
    )
    {
        (ConfigSchema? schema, IResult? error) = ResolveSchema(state, schemaId);
        if (schema is null)
        {
            return error!;
        }

        if (state.Options.OnSave is null)
        {
            return Results.NotFound(
                new { error = $"Schema '{schema.Id}' has no document persistence configured." }
            );
        }

        string body;
        using (StreamReader reader = new(httpContext.Request.Body))
        {
            body = await reader.ReadToEndAsync(httpContext.RequestAborted);
        }

        ConfigDocumentParseResult parsed = engine.Parse(body, schema);
        if (parsed.JsonError is not null)
        {
            return Results.BadRequest(
                new DocumentSaveErrorResponse(
                    "The posted body is not valid JSON.",
                    parsed.JsonError,
                    [],
                    []
                )
            );
        }

        ConfigDocumentParseResult validated = engine.Validate(parsed.Document, schema);
        if (!validated.IsValid)
        {
            return Results.UnprocessableEntity(
                new DocumentSaveErrorResponse(
                    "The document failed validation.",
                    null,
                    validated.MissingRequiredKeys,
                    [
                        .. validated.InvalidValues.Select(e => new ValidationErrorResponse(
                            e.Key,
                            e.Message
                        )),
                    ]
                )
            );
        }

        RemoteDocumentMergeService? mergeService =
            services.GetService<RemoteDocumentMergeService>();
        if (mergeService is not null)
        {
            RemoteDocumentSaveOutcome remoteOutcome = await mergeService.SplitAndSaveAsync(
                validated.Document,
                httpContext.RequestAborted
            );
            if (!remoteOutcome.Success)
            {
                RemoteDocumentSaveFailure first = remoteOutcome.Failures[0];
                return Results.UnprocessableEntity(
                    new DocumentSaveErrorResponse(
                        $"Instance '{first.InstanceName}' rejected the save: {first.Error}",
                        null,
                        first.MissingRequiredKeys,
                        first.InvalidValues
                    )
                );
            }
        }

        string payload = engine.Serialize(validated.Document, schema);

        if (secrets.Enabled && state.Options.OnLoad is not null)
        {
            string? stored = await state.Options.OnLoad(schema.Id);
            payload = secrets.MergeForStore(schema, payload, stored);
        }

        try
        {
            await state.Options.OnSave(schema.Id, payload);
        }
#pragma warning disable CA1031 // The host save handler is untrusted; surface any failure as a structured 400 instead of a bare 500.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return Results.BadRequest(new DocumentSaveErrorResponse(ex.Message, null, [], []));
        }

        return Results.Ok(new { success = true });
    }

    private static async Task<IResult> GetDataAsync(
        string sectionId,
        [FromQuery] string? schemaId,
        IConfigForgeHostState state,
        IPluginCatalog catalog,
        IActionDispatcher dispatcher,
        IConfigDocumentEngine engine,
        ConfigSecretGateway secrets,
        IServiceProvider services,
        HttpContext httpContext
    )
    {
        (ConfigSchema? schema, IResult? error) = ResolveSchema(state, schemaId);
        if (schema is null)
        {
            return error!;
        }

        if (
            !schema.Categories.Any(c =>
                string.Equals(c.CollectionKey, sectionId, StringComparison.Ordinal)
            )
        )
        {
            return Results.NotFound(
                new
                {
                    error = $"Section '{sectionId}' is not a collection in schema '{schema.Id}'.",
                }
            );
        }

        ConfigDocument document = await LoadDocumentAsync(
            schema,
            state.Options,
            secrets,
            engine,
            services
        );

        if (catalog.TryGetCollectionLoader(sectionId, out _))
        {
            HttpActionContext context = new(
                document,
                services,
                httpContext.RequestAborted,
                sectionId
            );
            IReadOnlyList<ConfigDocument> entries = await dispatcher.DispatchCollectionLoaderAsync(
                sectionId,
                context
            );
            return Results.Ok(
                entries.Select(
                    (entry, index) =>
                        new CollectionEntryResponse(
                            index.ToString(CultureInfo.InvariantCulture),
                            Flatten(entry)
                        )
                )
            );
        }

        if (document[sectionId] is IDictionary<string, object?> map)
        {
            return Results.Ok(map.Select(kv => new CollectionEntryResponse(kv.Key, kv.Value)));
        }

        return Results.Ok(Array.Empty<CollectionEntryResponse>());
    }

    private static async Task<IResult> PostActionAsync(
        string actionId,
        ActionRequest? body,
        [FromQuery] string? schemaId,
        IConfigForgeHostState state,
        IPluginCatalog catalog,
        IActionDispatcher dispatcher,
        IConfigDocumentEngine engine,
        ConfigSecretGateway secrets,
        IServiceProvider services,
        HttpContext httpContext
    )
    {
        if (
            body?.Args is { } args
            && catalog.TryGetCapabilityAction(actionId, out var capabilityHandler)
        )
        {
            return await InvokeCapabilityActionAsync(
                capabilityHandler!,
                args,
                httpContext.RequestAborted
            );
        }

        (ConfigSchema? schema, IResult? error) = ResolveSchema(state, body?.SchemaId ?? schemaId);
        if (schema is null)
        {
            return error!;
        }

        if (!catalog.TryGetAction(actionId, out _))
        {
            return Results.NotFound(new { error = $"Action '{actionId}' is not registered." });
        }

        ActionDefinition? definition = schema.Actions.FirstOrDefault(a =>
            string.Equals(a.ActionId, actionId, StringComparison.Ordinal)
        );
        string? actionCategoryKey = definition?.CategoryKey is { Length: > 0 }
            ? definition.CategoryKey
            : definition?.Category;
        string? collectionKey = actionCategoryKey is { Length: > 0 } category
            ? schema
                .Categories.FirstOrDefault(c =>
                    string.Equals(c.EffectiveKey, category, StringComparison.Ordinal)
                )
                ?.CollectionKey
            : null;

        if (definition is { RequiresEntry: true } && body?.EntryKey is not { Length: > 0 })
        {
            return Results.BadRequest(
                new { error = $"Action '{actionId}' requires an 'entryKey' in the request body." }
            );
        }

        ConfigDocument document = await LoadDocumentAsync(
            schema,
            state.Options,
            secrets,
            engine,
            services
        );
        string currentFieldKey =
            collectionKey is { Length: > 0 } && body?.EntryKey is { Length: > 0 } entryKey
                ? $"{collectionKey}/{entryKey}"
                : string.Empty;

        IReadOnlyList<ConfigDocument>? collectionEntries = null;
        if (
            collectionKey is { Length: > 0 }
            && catalog.TryGetCollectionLoader(collectionKey, out _)
        )
        {
            HttpActionContext probe = new(
                document,
                services,
                httpContext.RequestAborted,
                collectionKey
            );
            collectionEntries = await dispatcher.DispatchCollectionLoaderAsync(
                collectionKey,
                probe
            );
        }

        HttpActionContext context = new(
            document,
            services,
            httpContext.RequestAborted,
            currentFieldKey,
            collectionKey,
            collectionEntries
        );
        await dispatcher.DispatchActionAsync(actionId, context);

        (string? message, ToastSeverity? severity) =
            context.Toasts.Count > 0 ? context.Toasts[^1] : ((string?, ToastSeverity?))(null, null);
        bool success = severity is null or ToastSeverity.Info or ToastSeverity.Success;

        return Results.Ok(new ActionResponse(success, message, severity?.ToString()));
    }

    private static async Task<IResult> InvokeCapabilityActionAsync(
        Func<IReadOnlyList<object?>, CancellationToken, Task<object?>> handler,
        IReadOnlyList<object?> args,
        CancellationToken cancellationToken
    )
    {
        try
        {
            object? result = await handler(args, cancellationToken);
            return Results.Ok(
                new
                {
                    success = true,
                    message = (string?)null,
                    result,
                }
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Results.Ok(
                new
                {
                    success = false,
                    message = ex.InnerException?.Message ?? ex.Message,
                    result = (object?)null,
                }
            );
        }
    }

    private static (ConfigSchema? Schema, IResult? Error) ResolveSchema(
        IConfigForgeHostState state,
        string? schemaId
    )
    {
        if (schemaId is { Length: > 0 })
        {
            return state.Schemas.TryGetValue(schemaId, out ConfigSchema? found)
                ? (found, null)
                : (
                    null,
                    Results.NotFound(new { error = $"Schema '{schemaId}' is not available." })
                );
        }

        if (state.Schemas.Count == 1)
        {
            return (state.Schemas.Values.First(), null);
        }

        if (state.Schemas.Count == 0)
        {
            return (null, Results.NotFound(new { error = "No schema is currently loaded." }));
        }

        return (
            null,
            Results.BadRequest(
                new
                {
                    error = "Multiple schemas are loaded; specify ?schemaId=.",
                    schemaIds = state.Schemas.Keys.ToArray(),
                }
            )
        );
    }

    private static async Task<ConfigDocument> LoadDocumentAsync(
        ConfigSchema schema,
        AspNetConfigForgeOptions options,
        ConfigSecretGateway secrets,
        IConfigDocumentEngine engine,
        IServiceProvider services
    )
    {
        ConfigDocument document = await LoadLocalDocumentAsync(schema, options, secrets, engine);
        services.GetService<RemoteDocumentMergeService>()?.ApplyRemoteValues(document);
        return document;
    }

    private static async Task<ConfigDocument> LoadLocalDocumentAsync(
        ConfigSchema schema,
        AspNetConfigForgeOptions options,
        ConfigSecretGateway secrets,
        IConfigDocumentEngine engine
    )
    {
        if (options.OnLoad is not null)
        {
            string? persisted = await options.OnLoad(schema.Id);
            if (persisted is not null)
            {
                persisted = secrets.RedactForEditor(schema, persisted);
                return engine.Parse(persisted, schema).Document;
            }
        }

        return new ConfigDocument();
    }

    private static ManifestCategory BuildCategory(
        CategoryElement category,
        ConfigSchema schema,
        IPluginCatalog catalog
    ) =>
        new(
            category.Label,
            category.Description,
            category.CollectionKey,
            category.CollectionEntryLabelKey,
            category.CollectionKey is { Length: > 0 } collectionKey
                ? CollectionEntryElements(schema, collectionKey)
                : ToManifestElements(category.Elements, schema),
            category.CollectionKey is { Length: > 0 } key
                ? catalog.TryGetCollectionLoader(key, out _)
                : null
        );

    private static IReadOnlyList<ManifestElement> CollectionEntryElements(
        ConfigSchema schema,
        string collectionKey
    )
    {
        if (
            !schema.Fields.TryGetValue(collectionKey, out FieldDefinition? mapField)
            || mapField.ValueField is not { } template
        )
        {
            return [];
        }

        IEnumerable<FieldDefinition> entryFields =
            string.Equals(template.ControlType, "object", StringComparison.Ordinal)
            && template.Children.Count > 0
                ? template.Children
                : [template];

        return [.. entryFields.Select(field => ControlElement(ToManifestField(field)))];
    }

    /// <summary>
    /// Converts a category's real <see cref="UiElement"/> layout tree into the equivalent
    /// <see cref="ManifestElement"/> tree, preserving every group/tab/layout wrapper so a remote
    /// consumer can reconstruct the identical visual grouping. A <c>Control</c> whose scope does
    /// not resolve to a known field (stale or unsupported) is dropped rather than emitted empty.
    /// </summary>
    private static List<ManifestElement> ToManifestElements(
        IReadOnlyList<UiElement> elements,
        ConfigSchema schema
    )
    {
        List<ManifestElement> result = [];
        foreach (UiElement element in elements)
        {
            if (string.Equals(element.Type, "Control", StringComparison.Ordinal))
            {
                if (
                    JsonFormsScope.ToKey(element.Scope) is { } key
                    && schema.Fields.TryGetValue(key, out FieldDefinition? field)
                )
                {
                    result.Add(ControlElement(ToManifestField(field)));
                }

                continue;
            }

            result.Add(
                new ManifestElement(
                    element.Type,
                    element.Label,
                    Field: null,
                    ToManifestElements(element.Elements, schema)
                )
            );
        }

        return result;
    }

    private static ManifestElement ControlElement(ManifestField field) =>
        new("Control", Label: null, field, Elements: []);

    private static ManifestField ToManifestField(FieldDefinition field) =>
        new(field.Key, field.ControlType, field.Title, field.Required, field.ReadOnly);

    private static ManifestAction BuildAction(ActionDefinition action) =>
        new(
            action.ActionId,
            action.Label,
            action.Category,
            action.Section,
            action.RequiresEntry,
            action.Variant,
            action.Position
        );

    private static Dictionary<string, object?> Flatten(ConfigDocument entry)
    {
        Dictionary<string, object?> values = new(StringComparer.Ordinal);
        foreach (string key in entry.Keys)
        {
            values[key] = entry[key];
        }

        return values;
    }
}
