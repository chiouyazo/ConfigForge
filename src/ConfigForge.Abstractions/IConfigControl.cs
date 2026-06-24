#if NET8_0_OR_GREATER
using Microsoft.AspNetCore.Components;

namespace ConfigForge.Abstractions;

/// <summary>
/// Marker interface that all custom Blazor control components must implement.
/// </summary>
/// <remarks>
/// Available on the net8.0 target only. It depends on
/// <see cref="EventCallback{T}"/> from Microsoft.AspNetCore.Components, which has
/// no netstandard2.1 surface. A plugin that ships a custom control therefore
/// targets net8.0 (and already references Blazor to author the component);
/// action-, loader-, and validator-only plugins remain netstandard2.1-clean.
/// </remarks>
public interface IConfigControl
{
    /// <summary>The resolved control descriptor from the schema.</summary>
    ControlDescriptor Control { get; set; }

    /// <summary>The current config document being edited.</summary>
    ConfigDocument Document { get; set; }

    /// <summary>Raised when the user changes the value of this control.</summary>
    EventCallback<FieldChangedArgs> OnFieldChanged { get; set; }
}
#endif
