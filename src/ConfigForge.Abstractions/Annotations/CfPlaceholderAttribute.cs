namespace ConfigForge.Abstractions.Annotations;

/// <summary>
/// Sets the placeholder (<c>x-cf.controls[path].placeholder</c>) shown in an empty
/// input.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class CfPlaceholderAttribute : Attribute
{
    /// <summary>Creates the attribute with the placeholder text.</summary>
    /// <param name="placeholder">The placeholder text.</param>
    public CfPlaceholderAttribute(string placeholder) => Placeholder = placeholder;

    /// <summary>The placeholder text.</summary>
    public string Placeholder { get; }
}
