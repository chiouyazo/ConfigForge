using ConfigForge.Abstractions;

namespace ConfigForge.Blazor.Tests;

/// <summary>
/// A minimal in-memory <see cref="IActionContext"/> for unit-testing the dispatcher
/// without a running Blazor circuit. Records toast calls for assertions.
/// </summary>
internal sealed class FakeActionContext : IActionContext
{
    public List<(string Message, ToastSeverity Severity)> Toasts { get; } = [];

    public string this[string fieldKey] => string.Empty;

    public string CurrentFieldKey { get; init; } = string.Empty;

    public IServiceProvider Services { get; } = new EmptyServiceProvider();

    public CancellationToken CancellationToken { get; init; } = CancellationToken.None;

    public Task ShowToastAsync(string message, ToastSeverity severity)
    {
        Toasts.Add((message, severity));
        return Task.CompletedTask;
    }

    public Task SetFieldValueAsync(string fieldKey, object? value) => Task.CompletedTask;

    public Task SetFieldOptionsAsync(string fieldKey, IReadOnlyList<SelectOption> options) =>
        Task.CompletedTask;

    public Task SetFieldLoadingAsync(string fieldKey, bool loading) => Task.CompletedTask;

    public Task SetFieldEnabledAsync(string fieldKey, bool enabled) => Task.CompletedTask;

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
