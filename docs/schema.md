# Schema reference

A ConfigForge schema is one JSON document with three top-level objects:

```json
{
  "schema":   { },
  "uiSchema": { },
  "x-cf":     { }
}
```

## schema

Standard JSON Schema Draft 7. Field types, constraints, `required`, `default`. ConfigForge uses it for validation and to infer control types.

```json
"schema": {
  "type": "object",
  "properties": {
    "endpoint_url": { "type": "string", "title": "Endpoint URL", "pattern": "^https?://.+" },
    "interval_minutes": { "type": "integer", "minimum": 1, "maximum": 1440, "default": 60 }
  },
  "required": ["endpoint_url"]
}
```

## uiSchema

Standard JsonForms UI Schema: layout and grouping. Supported element types: `Categorization`, `Category`, `VerticalLayout`, `HorizontalLayout`, `Group`, `Control`, `Label`.

Rules are supported with effects `HIDE`, `SHOW`, `DISABLE`, `ENABLE` and a schema-based condition:

```json
{
  "type": "Control",
  "scope": "#/properties/target_channel",
  "rule": {
    "effect": "DISABLE",
    "condition": { "scope": "#/properties/enabled", "schema": { "const": false } }
  }
}
```

## x-cf

ConfigForge's own block, for everything JsonForms doesn't cover.

```json
"x-cf": {
  "id": "my-product",
  "name": "My Product",
  "version": "1.0.0",
  "controls": { },
  "actions": [ ],
  "categories": { }
}
```

### controls

Per-field hints, keyed by property path (`parent/child` for nested fields):

| Key | Meaning |
|---|---|
| `type` | Override the inferred control type (see below). |
| `placeholder` | Placeholder text. |
| `tooltip` | Hover help next to the label. |
| `unit` | Unit label for number/slider. |
| `loaderId` | Plugin loader that fills a select's options. |
| `validatorId` | Plugin validator for the field. |
| `keyFormat` | For a `map`: `uuid` hides the key input and generates a GUID key per entry. |
| `tracked` | `false` excludes the field from the unsaved-changes state. |
| `section` | A titled box the field is grouped into within its tab. |

### Inline hints (`x-*` keywords)

The `x-cf.controls` map only reaches top-level and flattened-object fields. Fields **inside** a `oneof` variant, an `arrayobject` item, or a `map` value are built without it. To hint those, put the same values **inline on the property schema** with an `x-` prefix — they travel with the schema everywhere:

| Inline keyword | Same as control key |
|---|---|
| `x-control` | `type` |
| `x-tooltip` / `x-placeholder` / `x-unit` | `tooltip` / `placeholder` / `unit` |
| `x-loader` / `x-validator` | `loaderId` / `validatorId` |
| `x-key-format` | `keyFormat` |
| `x-section` | `section` |
| `x-tracked` | `tracked` |

```json
"clientSecret": { "type": "string", "x-control": "secret" }
```

An explicit `x-cf.controls` entry wins over an inline hint. (The C# [generator](generation.md) emits inline hints, which is why they work inside variants.)

### actions

Buttons, handled by a plugin (see [plugins](plugins.md)):

```json
"actions": [
  {
    "actionId": "my.testConnection",
    "label": "Test Connection",
    "icon": "plug",
    "variant": "primary",
    "placement": { "category": "Connection", "position": "bottom" }
  }
]
```

### categories

Per-category metadata, keyed by category label:

| Key | Effect |
|---|---|
| `icon` | Icon identifier shown next to the category in the sidebar. |
| `description` | Intro text shown at the top of the category. |
| `collection` | Turns the category into a **master/detail** view over the named `map` field (see below). |
| `collectionLabel` | For a collection category, the entry sub-key whose value labels each entry in the sidebar (e.g. `name`). Falls back to the entry key. |
| `collectionAddLabel` | For a collection category, the label of the "add" button (e.g. `Add shop`). |

#### Collection categories (master/detail in the sidebar)

A category that holds a single `map` field can render as **master/detail** instead of stacking every entry on one page: each map entry becomes a selectable sub-item in the sidebar (under the category), with an add button and a per-entry remove, and the canvas shows only the selected entry's form.

```json
"x-cf": {
  "categories": {
    "Connectors": {
      "collection": "connectors",
      "collectionLabel": "name",
      "collectionAddLabel": "Add connector"
    }
  }
}
```

Here `connectors` is a keyed `map` (typically `additionalProperties` = an `oneof`, so each entry is a typed connector). Adding an entry prompts for the type (the `oneof` variant) and a name; the name is written to the `collectionLabel` field so the entry is identifiable immediately. Removing asks for confirmation and takes effect on save. This is well suited to a list of shops, tenants, environments, or connectors — anything you'd otherwise cram into one long page.

## Control types

The control is inferred from the JSON Schema unless you set `x-cf.controls[key].type`.

| Type | Inferred from |
|---|---|
| `text` | `string` |
| `number` | `integer` / `number` |
| `slider` | `integer` / `number` with both `minimum` and `maximum` |
| `checkbox` | `boolean` |
| `checklist` | `array` of `enum` strings (rendered as toggle chips) |
| `taglist` | `array` of strings |
| `select` | `string` with `enum`, or a `loaderId` |
| `date` / `datetime` / `time` | `string` with `format` `date` / `date-time` / `time` |
| `color` | `string` with `format: color` |
| `map` | `object` with `additionalProperties` (add/rename/remove entries) |
| `oneof` | a `oneOf` of object schemas (a variant picker) |
| `arrayobject` | `array` whose `items` is an object schema |

These require an explicit `type` (not inferred): `textarea`, `password`, `secret`, `daterange`, `filepath`, `code`. Plugin-provided control types are referenced by their registered id, e.g. `"type": "my.catImage"`.

### secret

A write-only control for passwords/keys. It never renders the stored value: a set secret shows "•••• stored" with **Change**/**Clear**; a new value is typed once. The host masks the value on load and interprets it on save via `ConfigForgeSecret.StoredMarker` (see [ASP.NET hosting](aspnet.md)).

### oneof

For a polymorphic object (`oneOf` of object schemas). A dropdown selects the variant by its discriminator — the one property with a string `const` — and the chosen variant's fields render below (switching resets the object to the new type). If the variant's fields carry `x-section`, they render as tabs within the entry. Combine with `map` (`additionalProperties: { "oneOf": [...] }`) for a keyed collection of polymorphic entries.

## Generating documents

From a schema you can generate two starting documents:

- **Example**: every field filled with an illustrative value (uses `default`, then the first `enum`, then a type-appropriate sample). Valid against the schema.
- **Empty**: only fields that declare a `default`. Anything required without a default shows as an error on load, prompting the user to fill it in.
