using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace FiniteAutomatons.Services.Observability;

public sealed class ActivityFileWriter
{
    private readonly string path;
    private readonly Lock fileLock = new();
    private readonly ConcurrentQueue<string> recent = new();
    private const int RecentLimit = 200;

    public ActivityFileWriter(string path)
    {
        this.path = path ?? throw new ArgumentNullException(nameof(path));
        var dir = Path.GetDirectoryName(this.path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        ActivityListener listener = new()
        {
            ShouldListenTo = a => true,
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = a => WriteActivity(a, true),
            ActivityStopped = a => WriteActivity(a, false)
        };

        ActivitySource.AddActivityListener(listener);
    }

    public string FilePath => path;

    public IReadOnlyCollection<string> GetRecentEntries()
    {
        return [.. recent];
    }

    private void EnqueueRecent(string line)
    {
        recent.Enqueue(line);
        while (recent.Count > RecentLimit && recent.TryDequeue(out _)) { }
    }

    private void WriteActivity(Activity activity, bool started)
    {
        lock (fileLock)
        {
            var record = new
            {
                TraceId = activity.TraceId.ToString(),
                SpanId = activity.SpanId.ToString(),
                ParentSpanId = activity.ParentSpanId.ToString(),
                Name = activity.DisplayName,
                Kind = activity.Kind.ToString(),
                Start = activity.StartTimeUtc,
                activity.Duration,
                Tags = activity.TagObjects?.ToDictionary(t => t.Key, t => t.Value?.ToString() ?? string.Empty),
                Started = started
            };

            var line = JsonSerializer.Serialize(record);

            try
            {
                File.AppendAllLines(path, [line]);
            }
            catch
            {
            }

            EnqueueRecent(line);
        }
    }
}
