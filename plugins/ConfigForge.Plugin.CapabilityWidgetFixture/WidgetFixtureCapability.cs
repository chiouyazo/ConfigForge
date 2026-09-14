namespace ConfigForge.Plugin.CapabilityWidgetFixture;

/// <summary>
/// The "real", data-touching implementation of <see cref="IWidgetFixtureCapability"/>. Its
/// constructor and method both bump a static sentinel counter so a test can prove the loader
/// never runs either, mirroring <c>ConfigForge.Plugin.CapabilityFixture.FixtureCapability</c>.
/// </summary>
public sealed class WidgetFixtureCapability : IWidgetFixtureCapability
{
    private static int s_instantiationCount;
    private static int s_invocationCount;

    /// <summary>Bumped once per instantiation. Must stay zero when only the capability loader touches this assembly.</summary>
    public static int InstantiationCount => s_instantiationCount;

    /// <summary>Bumped once per real call. Must stay zero when only the capability loader touches this assembly.</summary>
    public static int InvocationCount => s_invocationCount;

    /// <summary>Instantiating this type is the sentinel violation the loader must never cause.</summary>
    public WidgetFixtureCapability() => Interlocked.Increment(ref s_instantiationCount);

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetEntriesAsync(
        CancellationToken cancellationToken = default
    )
    {
        Interlocked.Increment(ref s_invocationCount);
        return Task.FromResult<IReadOnlyList<string>>(["real entry"]);
    }
}
