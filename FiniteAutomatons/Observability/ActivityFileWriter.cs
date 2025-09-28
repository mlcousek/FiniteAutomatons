using System.Diagnostics;
using System.Text.Json;

namespace FiniteAutomatons.Observability;

public sealed class ActivityFileWriter
{
    private readonly string _path;
    private readonly object _lock = new();

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
            File.AppendAllLines(_path, new[] { line });
        }
    }
}
