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
| `ApplicationTitle` | `Configuration` | Shown in the header. |
| `ThemeProvider` | built-in neutral theme | See [theming](theming.md). |
| `OnSave` | none | `Func<string schemaId, string json, Task>`. Required in `Locked` mode. |
| `OnLoad` | none | `Func<string schemaId, Task<string?>>`. Return `null` to start empty. |

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
