using System.Text.Json;
using System.Diagnostics;

namespace FiniteAutomatons.Observability;

public sealed class FileAuditService : IAuditService
{
    private readonly string _path;
    private readonly object _lock = new();

    public FileAuditService(string path)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
    }

    public Task AuditAsync(string eventType, string message, IDictionary<string, string?>? data = null)
    {
        // Try to capture current activity trace id for correlation
        string? traceId = Activity.Current?.TraceId.ToString();

        var record = new
        {
            Timestamp = DateTime.UtcNow,
            EventType = eventType,
            Message = message,
            TraceId = traceId,
            Data = data
        };

        var line = JsonSerializer.Serialize(record);
        lock (_lock) { File.AppendAllLines(_path, new[] { line }); }
        return Task.CompletedTask;
    }
}
