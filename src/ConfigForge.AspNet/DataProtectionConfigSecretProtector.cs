using ConfigForge.Abstractions;
using Microsoft.AspNetCore.DataProtection;

namespace ConfigForge.AspNet;

/// <summary>
/// An <see cref="IConfigSecretProtector"/> backed by ASP.NET Core Data Protection. Protected
/// values carry a <c>cfsec:v1:</c> marker so they are told apart from plaintext, which lets a
/// configuration with a mix of already-protected and not-yet-migrated plaintext values
/// round-trip and be upgraded on the next save.
/// </summary>
/// <remarks>
/// Two processes that must read each other's protected values (for example a UI host that
/// writes the configuration and a service that reads it) have to share the same Data Protection
/// key ring: persist the keys to a common directory and set the same application name on both.
/// </remarks>
public sealed class DataProtectionConfigSecretProtector : IConfigSecretProtector
{
    private const string Marker = "cfsec:v1:";

    // The Data Protection purpose. Changing it invalidates previously protected values.
    private const string Purpose = "ConfigForge.Secrets.v1";

    private readonly IDataProtector _protector;

    /// <summary>Creates a protector from the application's Data Protection provider.</summary>
    /// <param name="provider">The Data Protection provider to derive the protector from.</param>
    public DataProtectionConfigSecretProtector(IDataProtectionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _protector = provider.CreateProtector(Purpose);
    }

    /// <inheritdoc />
    public string Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        return Marker + _protector.Protect(plaintext);
    }

    /// <inheritdoc />
    public string Unprotect(string protectedValue)
    {
        ArgumentNullException.ThrowIfNull(protectedValue);
        if (!IsProtected(protectedValue))
        {
            throw new ArgumentException(
                "Value is not a protected secret token.",
                nameof(protectedValue)
            );
        }

        return _protector.Unprotect(protectedValue[Marker.Length..]);
    }

    /// <inheritdoc />
    public bool IsProtected(string value) =>
        value is not null && value.StartsWith(Marker, StringComparison.Ordinal);
}
