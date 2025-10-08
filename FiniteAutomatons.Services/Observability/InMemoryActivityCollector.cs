using System.Diagnostics;
using System.Collections.Concurrent;

namespace FiniteAutomatons.Services.Observability;

public sealed class InMemoryActivityCollector
{
    private readonly ConcurrentQueue<ActivityRecord> entries = new();

    public void Add(Activity activity)
    {
        if (activity == null) return;

        var record = new ActivityRecord
        {
            TraceId = activity.TraceId.ToString(),
            SpanId = activity.SpanId.ToString(),
            ParentSpanId = activity.ParentSpanId.ToString(),
            Name = activity.DisplayName,
            Kind = activity.Kind.ToString(),
            Start = activity.StartTimeUtc,
            Duration = activity.Duration,
            Tags = activity.TagObjects?.ToDictionary(t => t.Key, t => t.Value?.ToString() ?? string.Empty)
        };

        entries.Enqueue(record);
    }

    public IReadOnlyCollection<ActivityRecord> GetAll() => [.. entries];

    public void Clear() { while (entries.TryDequeue(out _)) { } }
}

public sealed class ActivityRecord
{
    public string TraceId { get; set; } = string.Empty;
    public string SpanId { get; set; } = string.Empty;
    public string ParentSpanId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public DateTime Start { get; set; }
    public TimeSpan Duration { get; set; }
    public IDictionary<string, string>? Tags { get; set; }
}
