using Microsoft.AspNetCore.Http;
using System.Text;

namespace FiniteAutomatons.UnitTests.TestHelpers;

public sealed class MockSession : ISession
{
    private readonly Dictionary<string, byte[]> store = [];

    public bool IsAvailable => true;
    public string Id { get; } = Guid.NewGuid().ToString();
    public IEnumerable<string> Keys => store.Keys;

    public void Clear() => store.Clear();
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Remove(string key) => store.Remove(key);

    public void Set(string key, byte[] value) => store[key] = value;

    public bool TryGetValue(string key, out byte[] value)
    {
        if (store.TryGetValue(key, out var v)) { value = v; return true; }
        value = [];
        return false;
    }

    public string? GetString(string key)
    {
        if (!TryGetValue(key, out var bytes) || bytes.Length == 0) return null;
        return Encoding.UTF8.GetString(bytes);
    }

    public void SetString(string key, string value)
        => Set(key, Encoding.UTF8.GetBytes(value));
}

public sealed class NoOpLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
        TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    { }
}
