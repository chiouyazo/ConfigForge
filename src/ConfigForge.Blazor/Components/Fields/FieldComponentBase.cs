using System.Globalization;
using ConfigForge.Abstractions;
using Microsoft.AspNetCore.Components;

namespace ConfigForge.Blazor.Components.Fields;

/// <summary>
/// Shared base for the built-in field components. Carries the
/// <see cref="IConfigControl"/> parameters and centralises value reads and the
/// change-notification path.
/// </summary>
public abstract class FieldComponentBase : ComponentBase, IConfigControl
{
    /// <inheritdoc />
    [Parameter]
    [EditorRequired]
    public ControlDescriptor Control { get; set; } = new();

    /// <inheritdoc />
    [Parameter]
    [EditorRequired]
    public ConfigDocument Document { get; set; } = new();

    /// <inheritdoc />
    [Parameter]
    public EventCallback<FieldChangedArgs> OnFieldChanged { get; set; }

    /// <summary>True when the control must not be edited.</summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>The field key this control edits.</summary>
    protected string Key => Control.Key;

    /// <summary>The current raw value from the document.</summary>
    protected object? CurrentValue => Document[Key];

    /// <summary>The current value rendered as a string, or an empty string.</summary>
    protected string CurrentString => Document.GetString(Key);

    /// <summary>Raises <see cref="OnFieldChanged"/> with the new value for this field.</summary>
    /// <param name="value">The new value.</param>
    /// <returns>A task that completes when the callback has run.</returns>
    protected Task NotifyChangedAsync(object? value) =>
        OnFieldChanged.InvokeAsync(new FieldChangedArgs { Key = Key, Value = value });

    /// <summary>
    /// Reads a value from <see cref="ControlDescriptor.Options"/> as an invariant
    /// string, or null when the option is absent.
    /// </summary>
    /// <param name="optionKey">The option key.</param>
    /// <returns>The option as a string, or null.</returns>
    protected string? OptionString(string optionKey) =>
        Control.Options.TryGetValue(optionKey, out object? value)
            ? Convert.ToString(value, CultureInfo.InvariantCulture)
            : null;

    /// <summary>Parses a string as an invariant <see cref="double"/>, or null.</summary>
    /// <param name="raw">The raw string.</param>
    /// <returns>The parsed value, or null when not a number.</returns>
    protected static double? ParseDouble(string? raw) =>
        double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out double value)
            ? value
            : null;

    /// <summary>Reads the current value coerced to a <see cref="bool"/>.</summary>
    /// <returns>The boolean value, defaulting to false.</returns>
    protected bool CurrentBool =>
        CurrentValue switch
        {
            bool b => b,
            string s when bool.TryParse(s, out bool parsed) => parsed,
            _ => false,
        };
}
