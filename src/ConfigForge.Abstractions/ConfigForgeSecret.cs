using System.Globalization;

namespace ConfigForge.Abstractions;

/// <summary>
/// Conventions for write-only secret fields (control type <c>secret</c> and the
/// <c>secretlist</c> array of secrets).
/// </summary>
/// <remarks>
/// A host that stores secrets encrypted or hashed replaces the real value with
/// <see cref="StoredMarker"/> when it loads a document, so the plaintext never
/// reaches the browser. On save it interprets the field: <see cref="StoredMarker"/>
/// means "keep the stored secret unchanged", a non-empty value means "replace with
/// this new plaintext", and an empty string or null means "clear the secret".
/// <para>
/// For a list of secrets every stored element is redacted to an <em>indexed</em> marker
/// (<see cref="IndexedMarker"/>) that records its position in the stored array. That lets a
/// kept element be resolved back to its original stored value even after the list is reordered
/// or an element is removed, which a plain positional marker cannot do.
/// </para>
/// </remarks>
public static class ConfigForgeSecret
{
    /// <summary>
    /// Sentinel value marking that a secret is stored but its value is deliberately
    /// withheld from the editor. Distinctive enough that no real secret collides.
    /// </summary>
    public const string StoredMarker = "__configforge-secret-stored__";

    private const string IndexPrefix = StoredMarker + "#";

    /// <summary>
    /// The stored marker for element <paramref name="index"/> of a secret list, carrying the
    /// index so the element can be matched back to its stored value regardless of its current
    /// position.
    /// </summary>
    /// <param name="index">The element's position in the stored array.</param>
    /// <returns>The indexed stored marker.</returns>
    public static string IndexedMarker(int index) =>
        IndexPrefix + index.ToString(CultureInfo.InvariantCulture);

    /// <summary>Whether a value is a stored-secret marker (the plain marker or an indexed one).</summary>
    /// <param name="value">The value to test.</param>
    /// <returns>True when the value marks a withheld stored secret.</returns>
    public static bool IsStoredMarker(string? value) =>
        string.Equals(value, StoredMarker, StringComparison.Ordinal)
        || (value is not null && value.StartsWith(IndexPrefix, StringComparison.Ordinal));

    /// <summary>Reads the element index from an indexed stored marker.</summary>
    /// <param name="value">The marker value.</param>
    /// <param name="index">The parsed index when the value is an indexed marker.</param>
    /// <returns>True when <paramref name="value"/> is an indexed marker with a valid index.</returns>
    public static bool TryGetStoredIndex(string? value, out int index)
    {
        index = -1;
        return value is not null
            && value.StartsWith(IndexPrefix, StringComparison.Ordinal)
            && int.TryParse(
                value.AsSpan(IndexPrefix.Length),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out index
            );
    }
}
