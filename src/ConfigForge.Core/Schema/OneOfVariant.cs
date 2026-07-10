namespace ConfigForge.Core.Schema;

/// <summary>
/// One selectable shape of a <c>oneof</c> control (from a schema <c>oneOf</c> entry):
/// the discriminator value that selects it and the field templates for its shape.
/// </summary>
public sealed class OneOfVariant
{
    /// <summary>The discriminator value that selects this variant (e.g. <c>email</c>).</summary>
    public string DiscriminatorValue { get; init; } = string.Empty;

    /// <summary>A human label for the variant; defaults to the discriminator value.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// The field templates for this variant's shape, excluding the discriminator.
    /// Keys are relative to a single value and rebased onto <c>parentKey/childKey</c>.
    /// </summary>
    public IReadOnlyList<FieldDefinition> Children { get; init; } = [];
}
