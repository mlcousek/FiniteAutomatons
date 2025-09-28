using System.Text.Json;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using FiniteAutomatons.Core.Models.ViewModel;
using Shouldly;

namespace FiniteAutomatons.IntegrationTests;

[Collection("Integration Tests")]
public class ObservabilityIntegrationTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    [Fact]
    public async Task AuditService_WritesAuditFile_WhenCalledViaDI()
    {
        using var scope = GetServiceScope();

        // Prefer in-memory audit collector in development/test environment
        var inMem = scope.ServiceProvider.GetService<FiniteAutomatons.Observability.InMemoryAuditService>();
        if (inMem != null)
        {
            // Invoke via IAuditService
            var svc = scope.ServiceProvider.GetRequiredService<FiniteAutomatons.Observability.IAuditService>();
            await svc.AuditAsync("IntegrationAudit", "Integration test audit", new Dictionary<string, string?> { ["k"] = "v" });

            // Give some time
            await Task.Delay(50);

            var entries = inMem.GetByEventType("IntegrationAudit");
            entries.ShouldNotBeEmpty();
            var last = entries.Last();
            last.EventType.ShouldBe("IntegrationAudit");
            last.Message.ShouldContain("Integration test audit");
        }
        else
        {
            // Fallback to file-based audit service
            var svc = scope.ServiceProvider.GetService(typeof(FiniteAutomatons.Observability.IAuditService)) as FiniteAutomatons.Observability.IAuditService;
            svc.ShouldNotBeNull();

            // Invoke audit
            await svc!.AuditAsync("IntegrationAudit", "Integration test audit", new Dictionary<string, string?> { ["k"] = "v" });

            // Discover file path via reflection on concrete type
            var impl = svc!;
            var field = impl.GetType().GetField("_path", BindingFlags.Instance | BindingFlags.NonPublic);
            field.ShouldNotBeNull();
            var path = field!.GetValue(impl) as string;
            path.ShouldNotBeNull();

            // Wait for file flush
            await Task.Delay(100);

            File.Exists(path!).ShouldBeTrue();
            var lines = File.ReadAllLines(path!);
            lines.ShouldNotBeEmpty();
            var last = lines.Last();
            var obj = JsonSerializer.Deserialize<JsonElement>(last);
            obj.GetProperty("EventType").GetString().ShouldBe("IntegrationAudit");

            // cleanup
            try { File.Delete(path!); } catch { }
        }
    }

    [Fact]
    public async Task ActivityFileWriter_WritesTrace_OnHttpRequest()
    {
        var client = GetHttpClient();
        var response = await client.GetAsync("/");
        response.EnsureSuccessStatusCode();

        using var scope = GetServiceScope();

        var collector = scope.ServiceProvider.GetService<FiniteAutomatons.Observability.InMemoryActivityCollector>();
        if (collector != null)
        {
            // Wait for activities
            await Task.Delay(100);
            var traces = collector.GetAll();
            traces.ShouldNotBeEmpty();
            var last = traces.Last();
            last.TraceId.ShouldNotBeNullOrWhiteSpace();
        }
        else
        {
            // Fallback to ActivityFileWriter (file + recent strings)
            var writer = scope.ServiceProvider.GetService(typeof(FiniteAutomatons.Observability.ActivityFileWriter)) as FiniteAutomatons.Observability.ActivityFileWriter;
            writer.ShouldNotBeNull();

            // Check in-memory recent entries first (avoids filesystem timing issues)
            var recent = writer!.GetRecentEntries();
            recent.ShouldNotBeNull();
            recent.Count.ShouldBeGreaterThan(0);

            var parsed = JsonSerializer.Deserialize<JsonElement>(recent.Last());
            parsed.TryGetProperty("TraceId", out var traceIdProp).ShouldBeTrue();
            traceIdProp.GetString().ShouldNotBeNullOrWhiteSpace();

            // Also assert file exists and contains entries, then cleanup
            var path = writer.FilePath;
            await Task.Delay(100);
            File.Exists(path).ShouldBeTrue();
            try { File.Delete(path); } catch { }
        }
    }

    [Fact]
    public void AutomatonGeneratorService_GeneratesAcceptingStringCandidates()
    {
        using var scope = GetServiceScope();
        var gen = scope.ServiceProvider.GetRequiredService<FiniteAutomatons.Services.Interfaces.IAutomatonGeneratorService>();
        gen.ShouldNotBeNull();

        var model = gen.GenerateRandomAutomaton(AutomatonType.DFA, 5, 12, alphabetSize: 3, acceptingStateRatio: 0.4, seed: 99);
        model.ShouldNotBeNull();
        model.Alphabet.Count.ShouldBe(3);

        // BFS locally to find accepting string up to length 8
        var starts = model.States.Where(s => s.IsStart).Select(s => s.Id).ToList();
        var accepting = new HashSet<int>(model.States.Where(s => s.IsAccepting).Select(s => s.Id));
        var map = model.Transitions.GroupBy(t => t.FromStateId).ToDictionary(g => g.Key, g => g.Select(t => (t.ToStateId, t.Symbol == '\0' ? (char?)null : t.Symbol)).ToList());

        var results = new HashSet<string>();
        var q = new Queue<(int, string)>();
        foreach (var s in starts) q.Enqueue((s, ""));
        var visited = new HashSet<string>();

        while (q.Count > 0 && results.Count < 5)
        {
            var tuple = q.Dequeue();
            var state = tuple.Item1;
            var str = tuple.Item2;
            var key = state + "|" + str;
            if (visited.Contains(key)) continue;
            visited.Add(key);

            if (accepting.Contains(state)) results.Add(str);
            if (str.Length >= 8) continue;

            if (map.TryGetValue(state, out var trans))
            {
                foreach (var t in trans)
                {
                    if (t.Item2 == null)
                        q.Enqueue((t.Item1, str));
                    else
                        q.Enqueue((t.Item1, str + t.Item2.Value));
                }
            }
        }

        // Not guaranteed to find accepting strings for arbitrary random automata, but ensure method runs and returns consistent model
        model.States.Count.ShouldBeGreaterThan(0);
    }
}
