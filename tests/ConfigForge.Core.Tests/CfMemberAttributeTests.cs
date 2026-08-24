using System.Text.Json.Nodes;
using ConfigForge.Abstractions.Annotations;
using ConfigForge.Core.Schema.Generation;
using ConfigForge.Core.Tests;
using Xunit;

// Assembly-level metadata for external types that cannot be annotated inline. These apply to
// the whole test assembly, which is exactly what the generator scans (the root type's assembly).
[assembly: CfMember(
    typeof(ExternalWidget),
    nameof(ExternalWidget.Level),
    Label = "Log Level",
    Order = 1
)]
[assembly: CfMember(typeof(ExternalWidget), nameof(ExternalWidget.Token), Secret = true)]
[assembly: CfMember(typeof(ExternalWidget), nameof(ExternalWidget.Hidden), Ignore = true)]
[assembly: CfMember(typeof(OwnedConfig), nameof(OwnedConfig.Name), Label = "From CfMember")]

namespace ConfigForge.Core.Tests;

/// <summary>Stands in for a library type the host cannot decorate inline.</summary>
internal sealed record ExternalWidget
{
    public string? Level { get; init; }
    public string? Token { get; init; }
    public string? Hidden { get; init; }
}

/// <summary>A type the host owns, used to prove inline attributes beat <c>[CfMember]</c>.</summary>
internal sealed record OwnedConfig
{
    [CfLabel("From Inline")]
    public string? Name { get; init; }
}

/// <summary>Root config referencing both an external and an owned type.</summary>
internal sealed record CfMemberRoot
{
    public ExternalWidget Widget { get; init; } = new();
    public OwnedConfig Owned { get; init; } = new();
}

/// <summary>
/// Assembly-level <c>[CfMember]</c> lets metadata be attached to properties of external types.
/// It is a fallback: an inline attribute on a property the host owns always wins.
/// </summary>
public sealed class CfMemberAttributeTests
{
    private static JsonNode SchemaProperties()
    {
        string json = new ClrSchemaGenerator().Generate<CfMemberRoot>(new() { Id = "root" });
        return JsonNode.Parse(json)!["schema"]!["properties"]!;
    }

    [Fact]
    public void Root_HasNestedExternalAndOwnedObjects()
    {
        CfMemberRoot root = new();
        Assert.NotNull(root.Widget);
        Assert.NotNull(root.Owned);
    }

    [Fact]
    public void CfMember_SuppliesLabelForExternalProperty()
    {
        JsonNode props = SchemaProperties();
        Assert.Equal("Log Level", (string?)props["widget"]!["properties"]!["level"]!["title"]);
    }

    [Fact]
    public void CfMember_MarksExternalPropertyAsSecret()
    {
        JsonNode props = SchemaProperties();
        Assert.Equal("secret", (string?)props["widget"]!["properties"]!["token"]!["x-control"]);
    }

    [Fact]
    public void CfMember_IgnoreExcludesExternalProperty()
    {
        JsonNode props = SchemaProperties();
        Assert.False(((JsonObject)props["widget"]!["properties"]!).ContainsKey("hidden"));
    }

    [Fact]
    public void InlineAttribute_BeatsCfMember()
    {
        JsonNode props = SchemaProperties();
        Assert.Equal("From Inline", (string?)props["owned"]!["properties"]!["name"]!["title"]);
    }
}
