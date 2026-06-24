# Build a standalone executable (Locked mode)

A standalone build is a self-contained Windows executable with the schema baked in. The user can't change the schema; they just edit the config for that one product. It's a WPF window hosting the same Blazor UI in a WebView.

[`src/ConfigForge.Standalone`](../src/ConfigForge.Standalone) is the reference host. To ship your own, follow the same shape.

## What you need

- Your schema, embedded as a resource.
- Any plugins, either compiled in or dropped in a `plugins/` folder next to the exe.
- `ConfigForge.Build` for the MSBuild targets and the plugin source generator.

## Embed the schema

Add your schema file and embed it under a `schemas/` logical name:

```xml
<ItemGroup>
  <EmbeddedResource Include="Schemas\my-product.json" LogicalName="schemas/my-product.json" />
</ItemGroup>
```

At startup the host reads the embedded schema, parses it, generates a starting document, and mounts the editor in Locked mode.

## Plugins

Two ways:

- **Compiled in**: reference the plugin project (or its package). `ConfigForge.Build`'s source generator finds every `IPlugin` at compile time and registers them, no reflection at runtime.
- **From a trusted path**: load DLLs from a `plugins/` folder next to the exe at startup. If you publish a single-file exe, keep the plugin DLLs loose (don't bundle them into the exe) so they can be loaded:

```xml
<Content Include="plugins\**\*.dll"
         CopyToOutputDirectory="PreserveNewest"
         CopyToPublishDirectory="PreserveNewest"
         ExcludeFromSingleFile="true" />
```

## Publish

```
dotnet publish src/ConfigForge.Standalone -c Release -r win-x64 --self-contained ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o out
```

You get `out/ConfigForge.Standalone.exe` plus an `out/plugins/` folder. Ship both together.

Requires WebView2 runtime on the target machine (present on current Windows 11; otherwise install the Evergreen runtime).
