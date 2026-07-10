namespace ConfigForge.Abstractions.Annotations;

/// <summary>
/// Assigns a top-level property to a named category (tab). Schema generation groups
/// categorised root properties into <c>uiSchema</c> categories in declaration order.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class CfCategoryAttribute : Attribute
{
    /// <summary>Creates the attribute with the category name.</summary>
    /// <param name="category">The category (tab) this property belongs to.</param>
    public CfCategoryAttribute(string category) => Category = category;

    /// <summary>The category (tab) name.</summary>
    public string Category { get; }
}
