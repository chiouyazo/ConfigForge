using ConfigForge.Core.Schema;
using Xunit;

namespace ConfigForge.Core.Tests;

public sealed class OneOfSchemaTests
{
    private const string Json = """
        {
          "schema": {
            "type": "object",
            "properties": {
              "Provider": {
                "oneOf": [
                  {
                    "type": "object",
                    "properties": {
                      "Type": { "const": "Alpha" },
                      "Url": { "type": "string" },
                      "AccessToken": { "type": "string" }
                    }
                  },
                  {
                    "type": "object",
                    "properties": {
                      "Type": { "const": "Beta" },
                      "Url": { "type": "string" },
                      "ClientId": { "type": "string" }
                    }
                  }
                ]
              }
            }
          },
          "x-cf": { "id": "oneof-test" }
        }
        """;

    private static ConfigSchema Parse() => new JsonFormsSchemaParser().Parse(Json);

    [Fact]
    public void Parse_ProducesOneOfControlWithDiscriminatorAndVariants()
    {
        ConfigSchema schema = Parse();
        FieldDefinition provider = schema.Fields["Provider"];

        Assert.Equal("oneof", provider.ControlType);
        Assert.Equal("Type", provider.DiscriminatorKey);
        Assert.Equal(2, provider.OneOfVariants.Count);

        OneOfVariant alpha = provider.OneOfVariants.Single(v => v.DiscriminatorValue == "Alpha");
        // The discriminator is excluded from the variant's editable fields.
        Assert.DoesNotContain(alpha.Children, c => c.Key == "Type");
        Assert.Contains(alpha.Children, c => c.Key == "AccessToken");

        OneOfVariant beta = provider.OneOfVariants.Single(v => v.DiscriminatorValue == "Beta");
        Assert.Contains(beta.Children, c => c.Key == "ClientId");
        Assert.DoesNotContain(beta.Children, c => c.Key == "AccessToken");
    }
}
