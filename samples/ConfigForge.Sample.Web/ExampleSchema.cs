namespace ConfigForge.Sample.Web;

/// <summary>The canonical example schema from the ConfigForge spec, used by the demo.</summary>
public static class ExampleSchema
{
    public const string Json = """
        {
          "schema": {
            "type": "object",
            "properties": {
              "endpoint_url": {
                "type": "string",
                "title": "Endpoint URL",
                "description": "Base URL of the target service including protocol",
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
              "endpoint_url": { "type": "text", "placeholder": "https://service.example.com" },
              "api_secret": { "type": "password" },
              "target_channel": { "type": "select", "loaderId": "example.loadChannels", "validatorId": "example.validateChannel" },
              "interval_minutes": { "type": "slider", "unit": "minutes" }
            },
            "actions": [
              {
                "actionId": "example.testConnection",
                "label": "Test Connection",
                "icon": "plug",
                "variant": "primary",
                "placement": { "category": "Connection", "position": "bottom" }
              }
            ],
            "categories": {
              "Connection": { "icon": "link", "description": "Connectivity and authentication settings" },
              "Synchronisation": { "icon": "sync", "description": "Synchronisation settings" }
            }
          }
        }
        """;
}
