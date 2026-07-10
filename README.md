# ConfigForge

ConfigForge renders a configuration UI from a [JsonForms](https://jsonforms.io/)-compatible schema. You give it a schema (field types, layout, and a few ConfigForge-specific hints) and it produces an editable form, validates input, and hands you back JSON. Custom controls and business logic come from plugins, so the core stays small.

It runs two ways:

- **Open mode**: ASP.NET middleware you add to an existing app. Good for internal tooling and schema authoring.
- **Locked mode**: a self-contained Windows executable with the schema baked in. Good for shipping a config editor for one product.

Both share the same engine and UI; only the host differs.

## Packages

| Package | What it's for |
|---|---|
| `ConfigForge.Abstractions` | The contracts you reference to write a plugin. Nothing else. |
| `ConfigForge.AspNet` | Add the UI to an ASP.NET app (Open mode). Pulls in everything else. |
| `ConfigForge.Build` | MSBuild targets + a source generator for building a standalone exe. |
| `ConfigForge.Core` | The schema engine, document handling, plugin loader. (Pulled in transitively.) |
| `ConfigForge.Blazor` | The Blazor components. (Pulled in transitively.) |

## Quick start (Open mode)

Add the package to an ASP.NET app:

```
dotnet add package ConfigForge.AspNet
```

Wire it up:

```csharp
builder.Services.AddConfigForge(options =>
{
    options.ApplicationTitle = "My Product";
    options.SchemaDirectory = "schemas";   // *.json schema files
    options.PluginDirectory = "plugins";   // plugin DLLs, loaded at startup
    options.OnSave = (schemaId, json) => File.WriteAllTextAsync($"{schemaId}.config.json", json);
    options.OnLoad = schemaId =>
        File.Exists($"{schemaId}.config.json")
            ? File.ReadAllTextAsync($"{schemaId}.config.json")
            : Task.FromResult<string?>(null);
});

// ...

app.UseConfigForge();
```

Drop a schema in `schemas/`, run the app, and the editor is served under `/config-ui`.

A full working host is in [`samples/ConfigForge.Sample.Web`](samples/ConfigForge.Sample.Web). Run it with:

```
dotnet run --project samples/ConfigForge.Sample.Web
```

## The schema

A schema is one JSON document with three parts:

```json
{
  "schema":   { },   // JSON Schema Draft 7: field types and constraints
  "uiSchema": { },   // JsonForms layout: categories, groups, rules
  "x-cf":     { }    // ConfigForge extras: control hints, actions, metadata
}
```

Standard JsonForms works as-is. The `x-cf` block adds things JsonForms doesn't cover (control type overrides, tooltips, units, loader/validator ids, action buttons). See [docs/schema.md](docs/schema.md).

You don't have to write the schema by hand: ConfigForge can **generate it from a C# type** by reflection — annotate the model with `[CfSecret]`, `[CfGroup]`, `[CfCategory]`, … and get a validated form. See [docs/generation.md](docs/generation.md).

## Documentation

- [Generate a schema from a C# type](docs/generation.md)
- [Add ConfigForge to an ASP.NET app](docs/aspnet.md)
- [Write a plugin](docs/plugins.md)
- [Build a standalone executable](docs/standalone.md)
- [Run in Docker](docs/docker.md)
- [Theming](docs/theming.md)
- [Schema reference](docs/schema.md)

## Building from source

```
dotnet build ConfigForge.slnx -c Release
dotnet test ConfigForge.slnx -c Release
```

Requires the .NET 10 SDK (pinned in `global.json`).

## License

Apache-2.0. See [LICENSE](LICENSE).
