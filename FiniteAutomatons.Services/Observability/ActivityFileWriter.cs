using System.Diagnostics;
using System.Text.Json;
using System.Collections.Concurrent;

namespace FiniteAutomatons.Observability;

public sealed class ActivityFileWriter
{
    private readonly string _path;
    private readonly object _lock = new();
    private readonly ConcurrentQueue<string> _recent = new();
    private const int RecentLimit = 200;

    public ActivityFileWriter(string path)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        // Subscribe to Activity events
        ActivityListener listener = new()
        {
            ShouldListenTo = a => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = a => WriteActivity(a, true),
            ActivityStopped = a => WriteActivity(a, false)
        };

        ActivitySource.AddActivityListener(listener);
    }

    // Expose path for tests and diagnostics
    public string FilePath => _path;

    // Expose recent in-memory entries for tests to avoid filesystem reliance
    public IReadOnlyCollection<string> GetRecentEntries()
    {
        return _recent.ToArray();
    }

    private void EnqueueRecent(string line)
    {
        _recent.Enqueue(line);
        while (_recent.Count > RecentLimit && _recent.TryDequeue(out _)) { }
    }

    private void WriteActivity(Activity activity, bool started)
    {
        lock (_lock)
        {
            var record = new
            {
                TraceId = activity.TraceId.ToString(),
                SpanId = activity.SpanId.ToString(),
                ParentSpanId = activity.ParentSpanId.ToString(),
                Name = activity.DisplayName,
                Kind = activity.Kind.ToString(),
                Start = activity.StartTimeUtc,
                Duration = activity.Duration,
                Tags = activity.TagObjects?.ToDictionary(t => t.Key, t => t.Value?.ToString() ?? string.Empty),
                Started = started
            };

            var line = JsonSerializer.Serialize(record);

            // Append to file
            try
            {
                File.AppendAllLines(_path, new[] { line });
            }
            catch
            {
                // Swallow I/O errors in production to avoid impacting app flow; tests will use in-memory entries.
            }

            // Keep in-memory cache
            EnqueueRecent(line);
        }
    }
}
