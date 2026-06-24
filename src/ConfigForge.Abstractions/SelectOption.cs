namespace ConfigForge.Abstractions;

/// <summary>A single option in a select or checklist control.</summary>
public sealed class SelectOption
{
    /// <summary>The value written to the config document when this option is selected.</summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>The label displayed to the user.</summary>
    public string Label { get; init; } = string.Empty;
}
