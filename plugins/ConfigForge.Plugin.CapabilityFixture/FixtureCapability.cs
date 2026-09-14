namespace ConfigForge.Plugin.CapabilityFixture;

/// <summary>
/// The "real", data-touching implementation of <see cref="IFixtureCapability"/>. Its constructor
/// and method both bump a static sentinel counter so a test can prove the loader never runs
/// either.
/// </summary>
public sealed class FixtureCapability : IFixtureCapability
{
    private static int s_instantiationCount;
    private static int s_invocationCount;

    /// <summary>Bumped once per instantiation. Must stay zero when only the capability loader touches this assembly.</summary>
    public static int InstantiationCount => s_instantiationCount;

    /// <summary>Bumped once per real call. Must stay zero when only the capability loader touches this assembly.</summary>
    public static int InvocationCount => s_invocationCount;

    /// <summary>Instantiating this type is the sentinel violation the loader must never cause.</summary>
    public FixtureCapability() => Interlocked.Increment(ref s_instantiationCount);

    /// <inheritdoc />
    public Task<string> EchoAsync(string value, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref s_invocationCount);
        return Task.FromResult(value);
    }
}
