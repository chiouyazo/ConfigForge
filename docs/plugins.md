# Write a plugin

A plugin adds behavior to a schema: button actions, dropdown data, field validation, and custom controls. It's a normal class library that references `ConfigForge.Abstractions` and nothing else from ConfigForge. You build it to a DLL and drop it in the host's plugin directory; the host loads it at startup.

The worked example for everything below is [`plugins/ConfigForge.Plugin.Showcase`](../plugins/ConfigForge.Plugin.Showcase).

## Project setup

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="ConfigForge.Abstractions" Version="0.1.0" />
  </ItemGroup>
</Project>
```

If your plugin ships a custom control (a Blazor component), also use the Razor SDK and reference the framework:

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <PackageReference Include="ConfigForge.Abstractions" Version="0.1.0" />
  </ItemGroup>
</Project>
```

## The entry point

Implement `IPlugin`. `Register` is called once at startup.

```csharp
public sealed class MyPlugin : IPlugin
{
    public string Id => "MyCompany.MyPlugin";
    public string DisplayName => "My Plugin";

    public void Register(IPluginRegistry registry)
    {
        registry.RegisterAction("my.testConnection", TestConnectionAsync);
        registry.RegisterLoader("my.loadChannels", LoadChannelsAsync);
        registry.RegisterValidator("my.channelRequired", ChannelRequired);
        registry.RegisterControl("my.catImage", typeof(CatImageControl));
    }
}
```

The ids (`my.testConnection`, etc.) are what you reference from the schema's `x-cf` block.

## Actions

An action is a button. Declare it in `x-cf.actions`, handle it here. The handler gets an `IActionContext` to read field values and change the UI.

```csharp
private static async Task TestConnectionAsync(IActionContext ctx)
{
    var client = new MyServiceClient(ctx["endpoint_url"], ctx["api_secret"]);
    await client.PingAsync(ctx.CancellationToken);
    await ctx.ShowToastAsync("Connection OK", ToastSeverity.Success);
}
```

`IActionContext` gives you: `ctx["fieldKey"]` (the current value as a string), `ctx.CurrentFieldKey` (the path of the field the handler runs for), `SetFieldValueAsync`, `SetFieldOptionsAsync`, `SetFieldLoadingAsync`, `SetFieldEnabledAsync`, `ShowToastAsync`, `Services` (the host's DI), and a `CancellationToken` that fires when the user leaves the category.

A button does nothing unless a plugin handles its id, which is the point: buttons are declared by the schema and powered by plugins, not built in.

## Loaders

A loader fills a `select` field at runtime (for example, channels fetched from an API). Reference it with `"loaderId"` on the control in `x-cf`.

```csharp
private static async Task<IReadOnlyList<SelectOption>> LoadChannelsAsync(IActionContext ctx)
{
    var channels = await new MyServiceClient(ctx["endpoint_url"], ctx["api_secret"])
        .GetChannelsAsync(ctx.CancellationToken);

    return channels.Select(c => new SelectOption { Value = c.Id, Label = c.Name }).ToList();
}
```

Set the loader from C# with `[CfLoader("my.loadChannels")]` (see [generation](generation.md)) or inline with `x-loader` on the property schema — both make the field a dropdown.

One loader can serve many fields: use `ctx.CurrentFieldKey` to see which field it's loading for and branch on the path. For example, a repeatable mapping under `connectors/{guid}/customerGroups/{i}/remote` can share a single loader that parses the key to pick the connector and the entity to fetch — instead of one loader per field.

## Validators

A validator checks one field's value. Reference it with `"validatorId"` on the control. It runs when the value changes and the message shows under the field.

```csharp
private static ValidationResult ChannelRequired(object? value) =>
    value is string s && !string.IsNullOrWhiteSpace(s)
        ? ValidationResult.Ok()
        : ValidationResult.Fail("Channel is required");
```

## Custom controls

A custom control is a Blazor component that implements `IConfigControl`. Register it with `RegisterControl("my.catImage", typeof(CatImageControl))` and point a field at it with `"type": "my.catImage"` in `x-cf.controls`.

```razor
@implements IConfigControl

<img src="@Document.GetString(Control.Key)" alt="" />

@code {
    [Parameter] public ControlDescriptor Control { get; set; } = new();
    [Parameter] public ConfigDocument Document { get; set; } = new();
    [Parameter] public EventCallback<FieldChangedArgs> OnFieldChanged { get; set; }
}
```

When the user edits, raise `OnFieldChanged` with the new value:

```csharp
await OnFieldChanged.InvokeAsync(new FieldChangedArgs { Key = Control.Key, Value = newValue });
```

Read the current value from `Document[Control.Key]` (or `Document.GetString(...)` for strings). Treat the document as the source of truth: read it on each render rather than caching, so the control and the document stay in sync.

**One gotcha:** a plugin project that ships `.razor` controls needs an `_Imports.razor` with the standard Blazor usings, or `@onclick` / `@onchange` won't wire up and your control will look fine but do nothing:

```razor
@using Microsoft.AspNetCore.Components
@using Microsoft.AspNetCore.Components.Web
@using ConfigForge.Abstractions
```

## Shipping it

Build the plugin and copy its output into the host's plugin directory (`PluginDirectory` in Open mode, or next to the exe in a `plugins/` folder for a standalone build). The host loads each plugin in its own collectible `AssemblyLoadContext` with an `AssemblyDependencyResolver`, so the plugin's private dependencies load from **its own folder**.

**What to exclude vs. ship — this matters, get it wrong and the plugin silently fails to load:**

- **Exclude (share from the host):** `ConfigForge.Abstractions` and the framework. Reference `ConfigForge.Abstractions` with `ExcludeAssets="runtime"` and use `<FrameworkReference Include="Microsoft.AspNetCore.App" />` (never a `PackageReference` copy). These *must* be the host's copy so your `IPlugin`/`IConfigControl`/`ComponentBase` are the same type the host expects. The loader always resolves `ConfigForge.*` from the host, and the framework resolves from the shared runtime.
- **Ship (they're yours):** every other third-party package you use at runtime — e.g. `MailKit`, `Microsoft.Data.SqlClient`. **Do not** blanket-set `ExcludeAssets="runtime"` or `CopyLocalLockFileAssemblies=false` on these; the host is not guaranteed to have them, and if it doesn't, `Assembly.GetTypes()` throws and the whole plugin (all its actions, controls, loaders) registers nothing. Let them copy to the plugin's output so the resolver can load them.

```xml
<ItemGroup>
  <!-- shared contract: host's copy, compile-only -->
  <PackageReference Include="ConfigForge.Abstractions" Version="…" ExcludeAssets="runtime" />
</ItemGroup>
<ItemGroup>
  <!-- your own deps: ship them (default behaviour, no ExcludeAssets) -->
  <PackageReference Include="MailKit" Version="…" />
</ItemGroup>
<ItemGroup>
  <FrameworkReference Include="Microsoft.AspNetCore.App" />
</ItemGroup>
```

If a plugin fails to load, the host logs why at the point of failure — `Plugin directory … does not exist` (wrong path), `No IPlugin implementations found` (usually a shared-contract copy shadowing the host's type), or `Failed to load types … Missing/unloadable dependencies: …` (a runtime dependency the plugin didn't ship and the host doesn't have). Check that message first.
