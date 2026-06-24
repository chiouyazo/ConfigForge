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

Per-field hints, keyed by property name:

| Key | Meaning |
|---|---|
| `type` | Override the inferred control type (see below). |
| `placeholder` | Placeholder text. |
| `tooltip` | Hover help next to the label. |
| `unit` | Unit label for number/slider. |
| `loaderId` | Plugin loader that fills a select's options. |
| `validatorId` | Plugin validator for the field. |

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

Optional per-category icon and description, keyed by category label.

## Control types

The control is inferred from the JSON Schema unless you set `x-cf.controls[key].type`.

| Type | Inferred from |
|---|---|
| `text` | `string` |
| `number` | `integer` / `number` |
| `slider` | `integer` / `number` with both `minimum` and `maximum` |
| `checkbox` | `boolean` |
| `checklist` | `array` of `enum` strings |
| `taglist` | `array` of strings |
| `select` | `string` with `enum`, or a `loaderId` |
| `date` / `datetime` / `time` | `string` with `format` `date` / `date-time` / `time` |
| `color` | `string` with `format: color` |

These require an explicit `x-cf` `type` (not inferred): `textarea`, `password`, `daterange`, `filepath`, `code`. Plugin-provided control types are referenced by their registered id, e.g. `"type": "my.catImage"`.

## Generating documents

From a schema you can generate two starting documents:

- **Example**: every field filled with an illustrative value (uses `default`, then the first `enum`, then a type-appropriate sample). Valid against the schema.
- **Empty**: only fields that declare a `default`. Anything required without a default shows as an error on load, prompting the user to fill it in.
