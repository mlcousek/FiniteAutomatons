using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace FiniteAutomatons.Observability;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _path;
    private readonly BlockingCollection<string> _queue = new();
    private readonly CancellationTokenSource _cts = new();

    public FileLoggerProvider(string path)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        Task.Run(ProcessQueue, _cts.Token);
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(_queue, categoryName);

    public void Dispose() { try { _cts.Cancel(); _queue.CompleteAdding(); } catch { } }

    private async Task ProcessQueue()
    {
        using var sw = new StreamWriter(new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read));
        foreach (var msg in _queue.GetConsumingEnumerable(_cts.Token))
        {
            await sw.WriteLineAsync(msg);
            await sw.FlushAsync();
        }
    }

    private sealed class FileLogger : ILogger
    {
        private readonly BlockingCollection<string> _queue;
        private readonly string _category;

        public FileLogger(BlockingCollection<string> queue, string category) { _queue = queue; _category = category; }

        IDisposable? ILogger.BeginScope<TState>(TState state) => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var line = $"[{DateTime.UtcNow:O}] {logLevel} {_category} - {formatter(state, exception)}";
            _queue.Add(line);
        }
    }
}
