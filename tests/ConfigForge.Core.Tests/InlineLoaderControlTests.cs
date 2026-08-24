using ConfigForge.Abstractions.Annotations;
using ConfigForge.Core.Schema;
using ConfigForge.Core.Schema.Generation;
using Xunit;

namespace ConfigForge.Core.Tests;

/// <summary>One row of a mapping collection: the connector-side id is loader-driven.</summary>
internal sealed record LoaderMappingRow
{
    [CfLoader("connector-entities")]
    public string? Remote { get; init; }

    [CfLoader("erp-entities")]
    public string? Local { get; init; }
}

/// <summary>A nullable object whose child is loader-driven.</summary>
internal sealed record LoaderFallback
{
    [CfLoader("connector-entities")]
    public string? Remote { get; init; }
}

internal sealed record LoaderRoot
{
    [CfLoader("jobs")]
    public string? Job { get; init; }

    [CfLoader("connector-entities")]
    public ICollection<string> Channels { get; init; } = [];

    public ICollection<string> FreeText { get; init; } = [];

    public ICollection<LoaderMappingRow> Mappings { get; init; } = [];

    public IDictionary<string, LoaderMappingRow> Keyed { get; init; } =
        new Dictionary<string, LoaderMappingRow>(StringComparer.Ordinal);

    public LoaderFallback? Fallback { get; init; }
}

/// <summary>
/// A <c>[CfLoader]</c> is emitted as the inline <c>x-loader</c> hint, which is the only channel
/// that survives into oneof variants, array items and map values (those are parsed without a
/// controls map). Control-type inference must therefore honour the inline hint, or a loader-driven
/// field renders as a plain text box and its fetched options are unreachable.
/// </summary>
public sealed class InlineLoaderControlTests
{
    private static ConfigSchema Parse()
    {
        string json = new ClrSchemaGenerator().Generate<LoaderRoot>(new() { Id = "root" });
        return new JsonFormsSchemaParser().Parse(json);
    }

    [Fact]
    public void Root_ExposesTheLoaderDrivenShapes()
    {
        LoaderRoot root = new()
        {
            Mappings = [new LoaderMappingRow()],
            Fallback = new LoaderFallback(),
        };

        Assert.Single(root.Mappings);
        Assert.NotNull(root.Fallback);
        Assert.Empty(root.Keyed);
    }

    [Fact]
    public void InlineLoader_OnScalar_RendersAsSelect()
    {
        FieldDefinition job = Parse().Fields["job"];

        Assert.Equal("select", job.ControlType);
        Assert.Equal("jobs", job.LoaderId);
    }

    [Fact]
    public void InlineLoader_OnStringCollection_RendersAsChecklist()
    {
        FieldDefinition channels = Parse().Fields["channels"];

        Assert.Equal("checklist", channels.ControlType);
        Assert.Equal("connector-entities", channels.LoaderId);
    }

    [Fact]
    public void StringCollection_WithoutLoader_StaysATagList()
    {
        Assert.Equal("taglist", Parse().Fields["freeText"].ControlType);
    }

    [Fact]
    public void InlineLoader_OnArrayItemProperty_RendersAsSelect()
    {
        FieldDefinition mappings = Parse().Fields["mappings"];
        Assert.Equal("arrayobject", mappings.ControlType);

        FieldDefinition remote = mappings.Children.Single(c => c.Key == "remote");
        FieldDefinition local = mappings.Children.Single(c => c.Key == "local");

        Assert.Equal("select", remote.ControlType);
        Assert.Equal("connector-entities", remote.LoaderId);
        Assert.Equal("select", local.ControlType);
        Assert.Equal("erp-entities", local.LoaderId);
    }

    [Fact]
    public void InlineLoader_OnMapValueProperty_RendersAsSelect()
    {
        FieldDefinition remote = Parse()
            .Fields["keyed"]
            .ValueField!.Children.Single(c => c.Key == "remote");

        Assert.Equal("select", remote.ControlType);
        Assert.Equal("connector-entities", remote.LoaderId);
    }

    [Fact]
    public void InlineLoader_OnNullableObjectChild_RendersAsSelect()
    {
        FieldDefinition fallback = Parse().Fields["fallback"];
        Assert.Equal("nullable-object", fallback.ControlType);

        FieldDefinition remote = fallback.Children.Single(c => c.Key == "remote");

        Assert.Equal("select", remote.ControlType);
        Assert.Equal("connector-entities", remote.LoaderId);
    }
}
