using FiniteAutomatons.Observability;
using Shouldly;
using System.Text.Json;

namespace FiniteAutomatons.UnitTests.Observability;

public class ObservabilityTests
{
    [Fact]
    public async Task FileAuditService_WritesJsonLine()
    {
        var path = Path.Combine(Path.GetTempPath(), $"audit_{Guid.NewGuid()}.log");
        if (File.Exists(path)) File.Delete(path);

        var svc = new FileAuditService(path);
        await svc.AuditAsync("TestEvent", "This is a test", new Dictionary<string, string?> { ["K"] = "V" });

        await Task.Delay(50);

        File.Exists(path).ShouldBeTrue();
        var lines = File.ReadAllLines(path);
        lines.Length.ShouldBeGreaterThan(0);
        var obj = JsonSerializer.Deserialize<JsonElement>(lines[0]);
        obj.GetProperty("EventType").GetString().ShouldBe("TestEvent");
        obj.GetProperty("Message").GetString().ShouldBe("This is a test");
        obj.GetProperty("Data").TryGetProperty("K", out var k).ShouldBeTrue();
        k.GetString().ShouldBe("V");

        File.Delete(path);
    }

    [Fact]
    public async Task MethodAuditor_CallsAuditStartAndEnd_OnSuccess()
    {
        var calls = new List<(string eventType, string message)>();
        var mock = new TestAudit(calls);

        var result = await MethodAuditor.AuditAsync(mock, "MyMethod", async () =>
        {
            await Task.Delay(10);
            return 42;
        });

        result.ShouldBe(42);
        calls.Count(c => c.eventType == "MethodStart").ShouldBe(1);
        calls.Count(c => c.eventType == "MethodEnd").ShouldBe(1);
    }

    [Fact]
    public async Task MethodAuditor_RecordsError_OnException()
    {
        var calls = new List<(string eventType, string message)>();
        var mock = new TestAudit(calls);

        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await MethodAuditor.AuditAsync(mock, "FailMethod", async () =>
            {
                await Task.Delay(10);
                throw new InvalidOperationException("boom");
            });
        });

        calls.Count(c => c.eventType == "MethodStart").ShouldBe(1);
        calls.Count(c => c.eventType == "MethodError").ShouldBe(1);
    }

    private sealed class TestAudit : IAuditService
    {
        private readonly List<(string eventType, string message)> _calls;
        public TestAudit(List<(string eventType, string message)> calls)
        {
            _calls = calls;
        }
        public Task AuditAsync(string eventType, string message, IDictionary<string, string?>? data = null)
        {
            _calls.Add((eventType, message));
            return Task.CompletedTask;
        }
    }
}
