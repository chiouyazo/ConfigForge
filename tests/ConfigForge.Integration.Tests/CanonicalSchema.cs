namespace ConfigForge.Integration.Tests;

/// <summary>
/// The canonical combined schema document from the ConfigForge spec, embedded as a
/// reusable fixture (a copy of the one used by ConfigForge.Core.Tests). Contains a
/// JSON Schema, a JsonForms Categorization uiSchema, and the <c>x-cf</c> block.
/// </summary>
internal static class CanonicalSchema
{
    public const string Json = """
        {
          "schema": {
            "type": "object",
            "properties": {
              "endpoint_url": {
                "type": "string",
                "title": "Endpoint URL",
                "pattern": "^https?://.+"
              },
              "api_secret": {
                "type": "string",
                "title": "API Secret"
              },
              "target_channel": {
                "type": "string",
                "title": "Target Channel"
              },
              "interval_minutes": {
                "type": "integer",
                "title": "Interval (minutes)",
                "minimum": 1,
                "maximum": 1440,
                "default": 60
              },
              "enabled": {
                "type": "boolean",
                "title": "Enabled",
                "default": true
              }
            },
            "required": ["endpoint_url", "api_secret"]
          },
          "uiSchema": {
            "type": "Categorization",
            "elements": [
              {
                "type": "Category",
                "label": "Connection",
                "elements": [
                  { "type": "Control", "scope": "#/properties/endpoint_url" },
                  { "type": "Control", "scope": "#/properties/api_secret" },
                  {
                    "type": "Control",
                    "scope": "#/properties/target_channel",
                    "rule": {
                      "effect": "DISABLE",
                      "condition": {
                        "scope": "#/properties/endpoint_url",
                        "schema": { "const": "" }
                      }
                    }
                  }
                ]
              },
              {
                "type": "Category",
                "label": "Synchronisation",
                "elements": [
                  { "type": "Control", "scope": "#/properties/interval_minutes" },
                  { "type": "Control", "scope": "#/properties/enabled" }
                ]
              }
            ]
          },
          "x-cf": {
            "id": "example-product",
            "name": "Example Product",
            "version": "1.0.0",
            "controls": {
              "api_secret": { "type": "password" },
              "target_channel": { "type": "select", "loaderId": "example.loadChannels" },
              "interval_minutes": { "type": "slider", "unit": "minutes" }
            },
            "categories": {
              "Connection": { "icon": "link", "description": "Connection settings" },
              "Synchronisation": { "icon": "sync", "description": "Sync settings" }
            }
          }
        }
        """;
}
