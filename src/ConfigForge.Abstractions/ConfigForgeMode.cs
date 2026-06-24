namespace ConfigForge.Abstractions;

/// <summary>Determines whether the user may change the active schema.</summary>
public enum ConfigForgeMode
{
    /// <summary>Schema selection and upload are allowed. Intended for developers and tooling.</summary>
    Open,

    /// <summary>The schema is fixed at startup and cannot be changed by the user.</summary>
    Locked,
}
