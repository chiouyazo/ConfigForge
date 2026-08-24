using System.Text.Json;
using ConfigForge.Core.Documents;
using Xunit;

namespace ConfigForge.Core.Tests;

/// <summary>
/// The value/JSON bridge behind the <c>code</c> control: object values round-trip through JSON
/// text, and invalid text surfaces as a <see cref="JsonException"/> the caller can catch instead
/// of committing a broken value.
/// </summary>
public sealed class ConfigValueJsonTests
{
    [Fact]
    public void Serialize_Object_ProducesJson()
    {
        Dictionary<string, object?> value = new(StringComparer.Ordinal)
        {
            ["name"] = "acme",
            ["count"] = 3,
            ["nested"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["on"] = true },
        };

        string json = ConfigValueJson.Serialize(value);
        object? parsed = ConfigValueJson.Parse(json);

        IDictionary<string, object?> roundTripped = Assert.IsAssignableFrom<
            IDictionary<string, object?>
        >(parsed);
        Assert.Equal("acme", roundTripped["name"]);
        IDictionary<string, object?> nested = Assert.IsAssignableFrom<IDictionary<string, object?>>(
            roundTripped["nested"]
        );
        Assert.Equal(true, nested["on"]);
    }

    [Fact]
    public void Serialize_Null_IsLiteralNull() =>
        Assert.Equal("null", ConfigValueJson.Serialize(null));

    [Fact]
    public void Parse_InvalidJson_Throws() =>
        Assert.ThrowsAny<JsonException>(() => ConfigValueJson.Parse("{ not valid"));
}
