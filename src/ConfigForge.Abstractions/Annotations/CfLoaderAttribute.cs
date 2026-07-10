namespace ConfigForge.Abstractions.Annotations;

/// <summary>
/// Marks a property as a dynamically-loaded select: its options are fetched at runtime
/// by the loader registered under <see cref="LoaderId"/>
/// (<see cref="IPluginRegistry.RegisterLoader"/>). Schema generation emits the loader id
/// as an inline hint and the field renders as a dropdown.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class CfLoaderAttribute : Attribute
{
    /// <summary>Creates the attribute with the loader id.</summary>
    /// <param name="loaderId">The id of a registered options loader.</param>
    public CfLoaderAttribute(string loaderId) => LoaderId = loaderId;

    /// <summary>The registered loader id that supplies this field's options.</summary>
    public string LoaderId { get; }
}
