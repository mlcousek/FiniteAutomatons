using System.Text.Json;
using System.Diagnostics;

namespace FiniteAutomatons.Observability;

public sealed class FileAuditService : IAuditService
{
    private readonly string path;
    private readonly Lock fileLock = new();

    public FileAuditService(string path)
    {
        this.path = path ?? throw new ArgumentNullException(nameof(path));
        var dir = Path.GetDirectoryName(this.path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
    }

    public Task AuditAsync(string eventType, string message, IDictionary<string, string?>? data = null)
    {
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
        lock (fileLock) { File.AppendAllLines(path, [line]); }
        return Task.CompletedTask;
    }
}
