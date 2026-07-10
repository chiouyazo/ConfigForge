# Generate a schema from a C# type

Instead of hand-writing a schema JSON, you can generate the whole `{ schema, uiSchema, x-cf }` document from a C# configuration type by reflection. The generated document is exactly what the parser consumes, so it can be registered in-memory, served, or written to disk. This keeps the editor in sync with your model: add a property, get a field.

```csharp
using ConfigForge.Core.Schema.Generation;

var generator = new ClrSchemaGenerator();   // or inject IClrSchemaGenerator
string json = generator.Generate<AppConfig>(new SchemaGenerationOptions
{
    Id = "app",
    Name = "App",
    Version = "1.0.0",
});
```

`IClrSchemaGenerator` is registered by `AddConfigForgeCore()`, so you can inject it too.

## A first model

```csharp
public sealed record AppConfig
{
    public string? InstanceName { get; init; }

    [CfSecret]
    public string? ApiPassword { get; init; }

    [CfControl("textarea")]
    public string? Notes { get; init; }

    public LogLevel Level { get; init; }          // enum -> select
    public bool Verbose { get; init; }            // -> checkbox
    public int RetentionDays { get; init; }       // -> number
}

public enum LogLevel { Verbose, Debug, Information, Warning, Error }
```

That produces a form with a text box, a write-only secret control, a multi-line box, a dropdown of the (camelCased) enum values, a checkbox, and a numeric field.

## How CLR types map

| CLR type | Result |
|---|---|
| `string` | `text` |
| `bool` | `checkbox` |
| `int`/`long`/… | `number` (integer) |
| `double`/`decimal` | `number` |
| `enum` | `select` with the enum values |
| `DateTime`/`DateTimeOffset` | `datetime` |
| `DateOnly` / `TimeOnly`/`TimeSpan` | `date` / `time` |
| `Guid` / `Uri` | string with `format` `uuid` / `uri` |
| `JsonNode` / `JsonElement` | `code` (free-form JSON) |
| `IReadOnlyList<T>` of a scalar | `taglist` (or `checklist` for enums) |
| `IReadOnlyList<T>` of an object | `arrayobject` (a repeatable sub-form) |
| `IDictionary<K,V>` / `IReadOnlyDictionary<K,V>` | `map` (add/rename/remove entries) |
| `Dictionary<Guid,V>` | `map` with hidden, auto-generated keys |
| a `[JsonPolymorphic]` base type | `oneof` (a variant picker on the discriminator) |
| any other class/record | a nested object, flattened into `parent/child` leaves |

Property names follow `SchemaGenerationOptions.PropertyNamingPolicy` (camelCase by default) or an explicit `[JsonPropertyName]`. `required` members / `[JsonRequired]` / `[Required]` become schema-required. `[Obsolete]` and `[JsonIgnore]` members are skipped.

## Attributes

All of these live in `ConfigForge.Abstractions.Annotations`. They only annotate; they add no runtime dependency on the UI.

| Attribute | Effect |
|---|---|
| `[CfControl("type")]` | Force a control type (e.g. `textarea`, `slider`, `code`, `tags`). |
| `[CfSecret]` | Render as the write-only secret control. |
| `[CfLoader("id")]` | Dynamic dropdown; options come from the registered loader (see [plugins](plugins.md)). |
| `[CfLabel("…")]` | Field label (schema `title`). |
| `[CfDescription("…")]` | Help text under the field. |
| `[CfTooltip("…")]` | Hover help on the label. |
| `[CfPlaceholder("…")]` / `[CfUnit("…")]` | Placeholder / unit suffix. |
| `[CfUntracked]` | Edits to this field don't count as unsaved changes. |
| `[CfIgnore]` | Exclude from the schema entirely. |
| `[CfOrder(n)]` | Sort order among siblings (lower first). |
| `[CfGroup("…")]` | Sidebar group (top-level navigation). |
| `[CfCategory("…")]` | Tab within a group (or a top-level tab if no groups). |
| `[CfSection("…")]` | A titled box (or a tab inside a `oneof` variant) grouping fields within a tab. |
| `[CfReadOnly]` *(via `[CfOptions]`)* | Render read-only (schema `readOnly`): shown but not editable. |
| `[CfEnableWhen("path", value)]` | Enable this field only while another field equals `value` (default `true`). |
| `[CfVisibleWhen("path", value)]` | Show this field only while another field equals `value`. |
| `[CfCategoryMeta("cat", Icon=…, Description=…)]` | **On the type.** Icon/description for a category. Repeatable. |
| `[CfAction("id", Label=…, Category=…, Icon=…)]` | **On the type.** Declares an action button; the handler is registered in code with the same id. Repeatable. |
| `[CfRow("id")]` | Lay adjacent fields sharing the id side by side (a `HorizontalLayout`) instead of stacked. |

### Validation: use standard DataAnnotations (no ConfigForge attribute needed)

There is intentionally **no `[CfRange]`**. Numeric bounds, string length, patterns and required-ness come from the standard `System.ComponentModel.DataAnnotations` (and `System.Text.Json`) attributes, which the generator reads and emits as JSON Schema constraints — ConfigForge then validates against them:

| Standard attribute | Emitted schema |
|---|---|
| `[Range(1, 65535)]` | `minimum` / `maximum` |
| `[StringLength(50, MinimumLength = 3)]` | `maxLength` / `minLength` |
| `[RegularExpression("…")]` | `pattern` |
| `[Required]` / `[JsonRequired]` / `required` keyword | added to the schema's `required` array |
| `[Description]` / `[Display(Name=…, Description=…)]` | `description` / `title` (fallbacks for `[CfLabel]`/`[CfDescription]`) |

```csharp
[Range(1, 65535)]
public int SmtpPort { get; init; } = 587;   // validated 1..65535, no extra ConfigForge attribute
```

### `[CfOptions]` — set several at once

When a property needs many of the above, stacking six attributes is noisy. `[CfOptions]` sets them all in one place with named arguments — each maps to the same result as its dedicated attribute:

```csharp
// these two are equivalent:
[CfGroup("Settings"), CfCategory("General"), CfSection("Identity"), CfOrder(0)]
[CfLabel("Instance name"), CfTooltip("Shown in logs.")]
public string? InstanceName { get; init; }

[CfOptions(
    Group = "Settings", Category = "General", Section = "Identity", Order = 0,
    Label = "Instance name", Tooltip = "Shown in logs.", ReadOnly = true)]
public string? InstanceName { get; init; }
```

Available settings: `Group`, `Category`, `Section`, `Order`, `Label`, `Description`, `Tooltip`, `Placeholder`, `Unit`, `Control`, `Loader`, `Secret`, `Tracked`, `ReadOnly`, `Ignore`.

The individual attributes still work **and take precedence** — set the common hints in `[CfOptions]` and override a single facet with a specific attribute (e.g. a dedicated `[CfLabel]` wins over `CfOptions.Label`). Tip: for a consistent app, keep the group/category/section names in `const string` fields and reference them (`Group = Layout.Settings`) instead of repeating string literals.

### Secret types by name

A property whose type is a library "secret wrapper" can be treated as a secret without `[CfSecret]` — register the type names:

```csharp
var options = new SchemaGenerationOptions { Id = "app" };
options.SecretTypeNames.Add("ProtectedValue");   // your encrypted/hashed wrapper type
```

The set is empty by default, so ConfigForge stays free of any specific library's type names.

## Layout: groups, tabs, sections

The navigation has up to three levels:

- `[CfGroup]` → an entry in the left sidebar.
- `[CfCategory]` → a tab shown when that group is selected.
- `[CfSection]` → a titled box within a tab (fields sharing a section are boxed together); works at any nesting depth (see below).

```csharp
public sealed record AppConfig
{
    [CfGroup("Settings"), CfCategory("General"), CfSection("Identity")]
    public string? InstanceName { get; init; }

    [CfGroup("Settings"), CfCategory("General"), CfSection("Identity")]
    [CfSecret]
    public string? ApiPassword { get; init; }

    [CfGroup("Settings"), CfCategory("Logging"), CfSection("Log")]
    public LogConfig? Log { get; init; }

    [CfGroup("Connections")]
    public IDictionary<Guid, Connection> Connections { get; init; } = new Dictionary<Guid, Connection>();
}
```

With only `[CfCategory]` (no groups) you get a single-level tab strip. With neither, a flat page.

`[CfGroup]`/`[CfCategory]` are read from the **top-level** properties (they define the outer navigation). `[CfSection]`, however, works at **any depth**: put it on a nested object's property and that object renders as its own titled box, e.g. a nested `Features` object becomes a "Features" box within its parent's tab — you are not limited to sectioning root fields.

```csharp
public sealed record ExchangeLock
{
    [CfSection("Features")]           // → a "Features" box, even though this is nested
    public FeatureToggles Features { get; init; } = new();

    public string? Note { get; init; }   // un-sectioned sibling: a bare field next to the box
}
```

Inside a `oneof` variant, `[CfSection]` on the variant's properties turns into tabs within that entry — useful for a polymorphic type with many fields.

## Polymorphism → oneof

A `System.Text.Json` polymorphic base becomes a variant picker. The discriminator is taken from `[JsonPolymorphic]`, and each `[JsonDerivedType]` is a variant:

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(HttpConnection), "http")]
[JsonDerivedType(typeof(FtpConnection), "ftp")]
public abstract record Connection
{
    public string? Name { get; init; }
}
```

A `Dictionary<Guid, Connection>` then renders as a keyless map where each entry has a `type` dropdown that switches its fields.

## Mixed mode: the overlay

Anything the reflection pass can't infer, hand-tune with an `Overlay` — a JSON fragment deep-merged over the generated document (objects merge; scalars/arrays replace):

```csharp
options.Overlay = new JsonObject
{
    ["schema"] = new JsonObject
    {
        ["properties"] = new JsonObject
        {
            ["log"] = new JsonObject
            {
                ["properties"] = new JsonObject
                {
                    ["level"] = new JsonObject { ["title"] = "Log level" },
                },
            },
        },
    },
    ["x-cf"] = new JsonObject
    {
        ["controls"] = new JsonObject
        {
            ["log/level"] = new JsonObject { ["tooltip"] = "How much to log." },
        },
    },
};
```

This is the escape hatch for labelling or hinting fields on types you can't annotate (e.g. library types).

### What still needs the overlay (no attribute)

Actions, category icons/descriptions, conditional enable/show rules, and side-by-side rows are all attributes now (see the table above). What remains overlay-only:

- **Labels/hints on library types you can't annotate** — the original reason the overlay exists. Use `x-cf.controls[path]` and `schema.properties.…title`.
- **Bespoke `uiSchema` structure** beyond what the attributes express (arbitrary nesting of layouts, etc.). ⚠️ The overlay **replaces** arrays rather than merging them, so hand-writing `uiSchema` discards the generated layout for that branch — rarely worth it.

Note that `[CfEnableWhen]`/`[CfVisibleWhen]` emit an **inline** rule on the property (`x-rule`), which the parser reads per-field — so conditional rules survive the overlay merge and don't require hand-writing `uiSchema` (the array-replace trap the overlay otherwise falls into).

## Using the generated schema

Parse it, then register it with the host:

```csharp
var schema = new JsonFormsSchemaParser().Parse(json);
hostState.UpsertSchema(schema);   // IConfigForgeHostState, resolved from DI after Build()
```

No schema files on disk are required — a single embedded model is enough.

See also: [schema reference](schema.md), [ASP.NET hosting](aspnet.md), [plugins & loaders](plugins.md), [theming](theming.md).
