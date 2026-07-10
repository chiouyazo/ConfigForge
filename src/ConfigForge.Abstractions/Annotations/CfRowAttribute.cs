namespace ConfigForge.Abstractions.Annotations;

/// <summary>
/// Lays consecutive fields that share the same row id side by side (a JsonForms
/// <c>HorizontalLayout</c>) instead of stacking them. Purely cosmetic; the fields keep their
/// order and behaviour. Fields must be adjacent (same category/section) to share a row.
/// </summary>
/// <example>
/// <code>
/// [CfRow("retry")] public int RetryCount { get; init; }
/// [CfRow("retry")] public int RetryDelaySeconds { get; init; }
/// [CfRow("retry")] public int TimeoutSeconds { get; init; }   // rendered on one line
/// </code>
/// </example>
[AttributeUsage(
    AttributeTargets.Property | AttributeTargets.Field,
    AllowMultiple = false,
    Inherited = false
)]
public sealed class CfRowAttribute : Attribute
{
    /// <summary>Creates a row grouping with the given id.</summary>
    /// <param name="row">Fields sharing this id (and adjacent) render on one horizontal line.</param>
    public CfRowAttribute(string row) => Row = row;

    /// <summary>The row id shared by fields that should sit side by side.</summary>
    public string Row { get; }
}
