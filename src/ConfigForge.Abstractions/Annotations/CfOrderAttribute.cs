namespace ConfigForge.Abstractions.Annotations;

/// <summary>
/// Orders a property relative to its siblings. Lower values sort first; properties
/// without the attribute keep their declaration order after ordered ones.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class CfOrderAttribute : Attribute
{
    /// <summary>Creates the attribute with the sort order.</summary>
    /// <param name="order">The sort key; lower sorts earlier.</param>
    public CfOrderAttribute(int order) => Order = order;

    /// <summary>The sort key.</summary>
    public int Order { get; }
}
