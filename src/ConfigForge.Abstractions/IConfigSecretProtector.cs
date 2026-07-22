namespace ConfigForge.Abstractions;

/// <summary>
/// Reversibly protects secret configuration values at rest. A host registers an
/// implementation (for example one backed by ASP.NET Core Data Protection) so that
/// secret fields (control type <c>secret</c>) are stored encrypted instead of in
/// plaintext, and are transparently decrypted when the configuration is read.
/// </summary>
/// <remarks>
/// Implementations must be able to tell a value they produced apart from an arbitrary
/// plaintext (see <see cref="IsProtected"/>), so that a configuration containing a mix
/// of already-protected and not-yet-migrated plaintext values round-trips correctly and
/// plaintext is upgraded to protected on the next save.
/// </remarks>
public interface IConfigSecretProtector
{
    /// <summary>Encrypts a plaintext secret into a self-describing protected token.</summary>
    /// <param name="plaintext">The secret value to protect.</param>
    /// <returns>A protected token for which <see cref="IsProtected"/> returns true.</returns>
    string Protect(string plaintext);

    /// <summary>Decrypts a token previously produced by <see cref="Protect"/>.</summary>
    /// <param name="protectedValue">A token for which <see cref="IsProtected"/> is true.</param>
    /// <returns>The original plaintext.</returns>
    string Unprotect(string protectedValue);

    /// <summary>Whether <paramref name="value"/> is a token produced by <see cref="Protect"/>.</summary>
    /// <param name="value">The value to test.</param>
    /// <returns>True when the value is a protected token, false for plaintext.</returns>
    bool IsProtected(string value);
}
