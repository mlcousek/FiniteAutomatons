using System.Collections.Concurrent;
using System.Text.Json;
using System.Diagnostics;
using System.Linq;

namespace FiniteAutomatons.Observability;

public sealed class InMemoryAuditService : IAuditService
{
    private readonly ConcurrentQueue<AuditRecord> entries = new();

    public Task AuditAsync(string eventType, string message, IDictionary<string, string?>? data = null)
    {
        var record = new AuditRecord
        {
            Timestamp = DateTime.UtcNow,
            EventType = eventType,
            Message = message,
            Data = data != null ? new Dictionary<string, string?>(data) : null,
            TraceId = Activity.Current?.TraceId.ToString()
        };

        entries.Enqueue(record);
        return Task.CompletedTask;
    }

    public IReadOnlyCollection<AuditRecord> GetAll() => [.. entries];

    public IEnumerable<AuditRecord> GetByEventType(string eventType)
    {
        return [.. entries.Where(e => string.Equals(e.EventType, eventType, StringComparison.OrdinalIgnoreCase))];
    }

    public IEnumerable<AuditRecord> GetByTimeRange(DateTime fromUtc, DateTime toUtc)
    {
        return [.. entries.Where(e => e.Timestamp >= fromUtc && e.Timestamp <= toUtc)];
    }

    public IEnumerable<AuditRecord> GetByTraceId(string traceId)
    {
        if (string.IsNullOrEmpty(traceId)) return Array.Empty<AuditRecord>();
        return [.. entries.Where(e => string.Equals(e.TraceId, traceId, StringComparison.OrdinalIgnoreCase))];
    }

    public bool TryGetLatestByEventType(string eventType, out AuditRecord? record)
    {
        record = entries.Where(e => string.Equals(e.EventType, eventType, StringComparison.OrdinalIgnoreCase)).LastOrDefault();
        return record != null;
    }

    public bool TryGetLatestByTraceId(string traceId, out AuditRecord? record)
    {
        if (string.IsNullOrEmpty(traceId))
        {
            record = null;
            return false;
        }
        record = entries.Where(e => string.Equals(e.TraceId, traceId, StringComparison.OrdinalIgnoreCase)).LastOrDefault();
        return record != null;
    }

    public void Clear() { while(entries.TryDequeue(out _)) { } }
}

public sealed class AuditRecord
{
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public IDictionary<string, string?>? Data { get; set; }
    public string? TraceId { get; set; }
}
