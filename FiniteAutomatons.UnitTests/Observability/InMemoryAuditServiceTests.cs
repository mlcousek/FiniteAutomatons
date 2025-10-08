using System;
using System.Linq;
using FiniteAutomatons.Observability;
using Shouldly;
using Xunit;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;

namespace FiniteAutomatons.UnitTests.Observability;

public class InMemoryAuditServiceTests
{
    [Fact]
    public async Task AuditAsync_StoresRecords_AndQueryMethodsWork()
    {
        var svc = new InMemoryAuditService();

        // Ensure no entries initially
        svc.GetAll().Count.ShouldBe(0);

        await svc.AuditAsync("EventA", "MessageA");
        await svc.AuditAsync("EventB", "MessageB", new Dictionary<string, string?> { ["K"] = "V" });

        var all = svc.GetAll();
        all.Count.ShouldBe(2);

        var evA = svc.GetByEventType("EventA").ToArray();
        evA.Length.ShouldBe(1);
        evA[0].Message.ShouldBe("MessageA");

        var evB = svc.GetByEventType("EventB").FirstOrDefault();
        evB.ShouldNotBeNull();
        evB.Data.ShouldNotBeNull();
        evB.Data["K"].ShouldBe("V");

        // Time range
        var from = DateTime.UtcNow.AddMinutes(-1);
        var to = DateTime.UtcNow.AddMinutes(1);
        var byTime = svc.GetByTimeRange(from, to).ToArray();
        byTime.Length.ShouldBeGreaterThanOrEqualTo(2);

        // TryGetLatestByEventType
        svc.TryGetLatestByEventType("EventB", out var latestB).ShouldBeTrue();
        latestB.ShouldNotBeNull();
        latestB!.Message.ShouldBe("MessageB");

        // TraceId test
        using var activity = new Activity("test");
        activity.Start();
        await svc.AuditAsync("TraceEvent", "Tmsg");
        var traceId = Activity.Current?.TraceId.ToString();
        svc.TryGetLatestByTraceId(traceId!, out var byTrace).ShouldBeTrue();
        byTrace.ShouldNotBeNull();
        byTrace!.Message.ShouldBe("Tmsg");
        activity.Stop();

        // Clear
        svc.Clear();
        svc.GetAll().Count.ShouldBe(0);
    }
}
