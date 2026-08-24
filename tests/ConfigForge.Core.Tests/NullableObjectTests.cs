using System.Text.Json.Nodes;
using ConfigForge.Core.Schema;
using ConfigForge.Core.Schema.Generation;
using Xunit;

namespace ConfigForge.Core.Tests;

internal sealed record InnerBlock
{
    public string? Text { get; init; }
    public int Number { get; init; }
}

internal sealed record NullableHost
{
    /// <summary>Nullable object → should render with an enable/disable toggle.</summary>
    public InnerBlock? Optional { get; init; }

    /// <summary>Non-nullable object → flattened as usual, no toggle.</summary>
    public InnerBlock Required { get; init; } = new();
}

/// <summary>
/// A nullable object property is emitted as an <c>x-control: nullable-object</c> field with its
/// children, so the UI can toggle the whole section on/off (null). Non-nullable objects are
/// flattened as before.
/// </summary>
public sealed class NullableObjectTests
{
    private static string Json() =>
        new ClrSchemaGenerator().Generate<NullableHost>(new() { Id = "host" });

    [Fact]
    public void Host_CanBeInstantiated()
    {
        NullableHost host = new();
        Assert.NotNull(host.Required);
    }

    [Fact]
    public void NullableObject_GetsNullableObjectControl()
    {
        JsonNode props = JsonNode.Parse(Json())!["schema"]!["properties"]!;
        Assert.Equal("nullable-object", (string?)props["optional"]!["x-control"]);
    }

    [Fact]
    public void NonNullableObject_HasNoToggleControl()
    {
        JsonNode props = JsonNode.Parse(Json())!["schema"]!["properties"]!;
        Assert.Null((string?)props["required"]!["x-control"]);
    }

    [Fact]
    public void ParsedNullableObject_KeepsControlTypeAndChildren()
    {
        ConfigSchema schema = new JsonFormsSchemaParser().Parse(Json());

        FieldDefinition optional = schema.Fields["optional"];
        Assert.Equal("nullable-object", optional.ControlType);
        Assert.Equal(2, optional.Children.Count);

        // The non-nullable object is flattened, so its children appear under a prefixed key.
        Assert.True(schema.Fields.ContainsKey("required/text"));
        Assert.False(schema.Fields.ContainsKey("required"));
    }
}
