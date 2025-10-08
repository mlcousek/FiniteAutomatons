using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace FiniteAutomatons.Services.Observability;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string path;
    private readonly BlockingCollection<string> queue = [];
    private readonly CancellationTokenSource cts = new();

    public FileLoggerProvider(string path)
    {
        this.path = path ?? throw new ArgumentNullException(nameof(path));
        var dir = Path.GetDirectoryName(this.path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        Task.Run(ProcessQueue, cts.Token);
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(queue, categoryName);

    public void Dispose() { try { cts.Cancel(); queue.CompleteAdding(); } catch { } }

    private async Task ProcessQueue()
    {
        using var sw = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read));
        foreach (var msg in queue.GetConsumingEnumerable(cts.Token))
        {
            await sw.WriteLineAsync(msg);
            await sw.FlushAsync();
        }
    }

    private sealed class FileLogger(BlockingCollection<string> queue, string category) : ILogger
    {
        private readonly BlockingCollection<string> queue = queue;
        private readonly string category = category;

        IDisposable? ILogger.BeginScope<TState>(TState state) => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var traceId = Activity.Current?.TraceId.ToString();
            var tracePart = string.IsNullOrWhiteSpace(traceId) ? "" : $" [Trace:{traceId}]";
            var line = $"[{DateTime.UtcNow:O}] {logLevel} {category}{tracePart} - {formatter(state, exception)}";
            queue.Add(line);
        }
    }
}
