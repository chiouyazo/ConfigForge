using System.Diagnostics.CodeAnalysis;

namespace ConfigForge.Core.Plugins;

/// <summary>
/// The read side consumed by <c>FieldRenderer</c> to resolve a mirrored field's control type to a
/// remote instance's widget, when that control type is not one of <c>IPluginCatalog</c>'s locally
/// registered controls.
/// </summary>
public interface ICapabilityWidgetCatalog
{
    /// <summary>Attempts to resolve a registered capability widget.</summary>
    /// <param name="controlType">The field's control type, as it appears on <c>FieldDefinition</c>.</param>
    /// <param name="registration">The registration if found.</param>
    /// <returns>True when a capability widget is registered for the control type.</returns>
    bool TryGetWidget(
        string controlType,
        [NotNullWhen(true)] out CapabilityWidgetRegistration? registration
    );
}
