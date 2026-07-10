# Add ConfigForge to an ASP.NET app (Open mode)

Open mode serves the editor as part of an ASP.NET app. Users pick a schema, edit it in the browser, and save. Schemas and plugins are loaded from directories on disk.

## Install

```
dotnet add package ConfigForge.AspNet
```

That brings in the Blazor UI and the engine transitively.

## Wire it up

There are two calls: one in service registration, one in the pipeline.

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConfigForge(options =>
{
    options.ApplicationTitle = "My Product";
    options.PathPrefix = "/config-ui";   // default
    options.SchemaDirectory = "schemas";
    options.PluginDirectory = "plugins";
    options.OnSave = (schemaId, json) => File.WriteAllTextAsync($"{schemaId}.config.json", json);
    options.OnLoad = schemaId =>
        File.Exists($"{schemaId}.config.json")
            ? File.ReadAllTextAsync($"{schemaId}.config.json")
            : Task.FromResult<string?>(null);
});

var app = builder.Build();

app.UseConfigForge();

app.Run();
```

`UseConfigForge()` sets up the interactive Blazor endpoints and serves the UI under `PathPrefix`.

## Options

| Option | Default | Notes |
|---|---|---|
| `PathPrefix` | `/config-ui` | Where the UI is served. |
| `SchemaDirectory` | `schemas` | Folder of `*.json` schema files. |
| `PluginDirectory` | `plugins` | Folder of plugin DLLs, loaded at startup. |
| `Mode` | `Open` | `Open` lets users choose/upload a schema; `Locked` fixes it. |
| `ApplicationTitle` | `Configuration` | Shown in the header (unless a top-left logo replaces it). |
| `ThemeProvider` | built-in neutral theme | See [theming](theming.md). |
| `ShowSchemaPicker` | `false` | Show the top-left schema dropdown. Leave off when embedding a single fixed schema. |
| `ShowCodePanel` | `false` | Show the "View code" (raw JSON/schema) button. |
| `HeaderActions` | empty | Custom header links (`ConfigForgeHeaderAction`: `Label`, `Url`, `OpenInNewTab`, `Variant`, optional `IconSvg` for an icon-only button). |
| `OnSave` | none | `Func<string schemaId, string json, Task>`. Required in `Locked` mode. |
| `OnLoad` | none | `Func<string schemaId, Task<string?>>`. Return `null` to start empty. |

`ShowSchemaPicker` and `ShowCodePanel` default off so ConfigForge doesn't add chrome to a host it's only a part of. Turn them on for a schema-authoring host.

## Running without publishing

`WebApplication.CreateBuilder` only enables Blazor's static web assets in the Development environment, so a non-published `dotnet run` in Production serves no `_framework`/`_content` assets — the interactive circuit never starts and the CSS is missing. Use the builder overload, which enables static web assets in any environment:

```csharp
builder.AddConfigForge(options => { /* ... */ });   // instead of builder.Services.AddConfigForge(...)
```

Published apps work either way.

## Secrets

A `secret` control is write-only. On load, replace the real value with the sentinel `ConfigForge.Abstractions.ConfigForgeSecret.StoredMarker` so the plaintext never reaches the browser; the control then shows "stored". On save, interpret the field:

- value == `StoredMarker` → keep the existing secret unchanged;
- non-empty other value → a new plaintext to (re-)encrypt/hash;
- empty/absent → clear it.

`OnLoad`/`OnSave` deal in the serialized document, so this is a small transform over the JSON (or over your strongly-typed config with a pair of converters).

## Registering a generated schema

Instead of schema files, you can [generate a schema from a C# type](generation.md) and register it at startup:

```csharp
var app = builder.Build();
var state = app.Services.GetRequiredService<IConfigForgeHostState>();
var json = new ClrSchemaGenerator().Generate<AppConfig>(new() { Id = "app", Name = "App" });
state.UpsertSchema(new JsonFormsSchemaParser().Parse(json));
app.UseConfigForge();
```

## On disk

```
your-app/
  schemas/
    my-product.json
  plugins/
    MyCompany.MyPlugin.dll
```

The directories are watched: adding or replacing a schema or plugin is picked up without a restart. If a plugin a schema depends on is missing, that schema still loads but its actions and loaders are disabled with a warning.

## In the browser

Users can pick a schema from the list, paste or upload a config JSON, generate an example or an empty document, edit, and save. Malformed JSON shows an inline editor so they can fix and re-parse it.
