using ConfigForge.Core.Schema.Generation;
using Xunit;

namespace ConfigForge.Core.Tests;

internal sealed record ConcurrentInner
{
    public string? Text { get; init; }
    public ConcurrentLeaf? Leaf { get; init; }
}

internal sealed record ConcurrentLeaf
{
    public int? Number { get; init; }
    public string? Name { get; init; }
}

internal sealed record ConcurrentRoot
{
    public ConcurrentInner? First { get; init; }
    public ConcurrentInner? Second { get; init; }
    public IDictionary<string, ConcurrentInner> Keyed { get; init; } =
        new Dictionary<string, ConcurrentInner>(StringComparer.Ordinal);
}

/// <summary>
/// Generation reads nullability through <see cref="System.Reflection.NullabilityInfoContext"/>,
/// which caches into a plain dictionary and is not thread-safe. Sharing one instance across threads
/// made concurrent generation throw from inside that cache, so the generator must keep them apart.
/// </summary>
public sealed class ConcurrentGenerationTests
{
    [Fact]
    public void Root_ExposesTheNestedNullableShapes()
    {
        ConcurrentRoot root = new() { First = new ConcurrentInner { Leaf = new ConcurrentLeaf() } };

        Assert.NotNull(root.First.Leaf);
        Assert.Null(root.Second);
        Assert.Empty(root.Keyed);
    }

    [Fact]
    public void GeneratingConcurrentlyProducesTheSameSchema()
    {
        string expected = new ClrSchemaGenerator().Generate<ConcurrentRoot>(new() { Id = "root" });

        string[] results = new string[64];
        Parallel.For(
            0,
            results.Length,
            index =>
                results[index] = new ClrSchemaGenerator().Generate<ConcurrentRoot>(
                    new() { Id = "root" }
                )
        );

        Assert.All(results, result => Assert.Equal(expected, result));
    }
}
