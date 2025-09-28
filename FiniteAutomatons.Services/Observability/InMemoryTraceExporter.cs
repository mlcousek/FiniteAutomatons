using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;

namespace FiniteAutomatons.Observability;

public sealed class InMemoryActivityCollector
{
    private readonly ConcurrentQueue<ActivityRecord> _entries = new();
    private readonly ActivityListener listener;

    public InMemoryActivityCollector()
    {
        listener = new ActivityListener
        {
            ShouldListenTo = s => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => Capture(activity, true),
            ActivityStopped = activity => Capture(activity, false)
        };

        ActivitySource.AddActivityListener(listener);
    }

    private void Capture(Activity activity, bool started)
    {
        var record = new ActivityRecord
        {
            TraceId = activity.TraceId.ToString(),
            SpanId = activity.SpanId.ToString(),
            Name = activity.DisplayName,
            Start = activity.StartTimeUtc,
            Duration = activity.Duration,
            Started = started,
            Tags = activity.TagObjects?.ToDictionary(t => t.Key, t => t.Value?.ToString())
        };

        _entries.Enqueue(record);
    }

    public IReadOnlyCollection<ActivityRecord> GetAll() => _entries.ToArray();

    public IEnumerable<ActivityRecord> GetByTraceId(string traceId)
    {
        if (string.IsNullOrEmpty(traceId)) return Array.Empty<ActivityRecord>();
        return _entries.Where(e => string.Equals(e.TraceId, traceId, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    public IEnumerable<ActivityRecord> GetByTimeRange(DateTime fromUtc, DateTime toUtc)
    {
        return _entries.Where(e => e.Start >= fromUtc && e.Start <= toUtc).ToArray();
    }

    public bool TryGetLatestByTraceId(string traceId, out ActivityRecord? record)
    {
        record = null;
        if (string.IsNullOrEmpty(traceId)) return false;
        record = _entries.Where(e => string.Equals(e.TraceId, traceId, StringComparison.OrdinalIgnoreCase)).LastOrDefault();
        return record != null;
    }

    public bool TryGetLatestByName(string name, out ActivityRecord? record)
    {
        record = _entries.Where(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase)).LastOrDefault();
        return record != null;
    }
}

public sealed class ActivityRecord
{
    public string TraceId { get; set; } = string.Empty;
    public string SpanId { get; set; } = string.Empty;
    public string? Name { get; set; }
    public DateTime Start { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Started { get; set; }
    public IDictionary<string, string?>? Tags { get; set; }
}
