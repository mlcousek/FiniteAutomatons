using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace FiniteAutomatons.IntegrationTests;

[Collection("Integration Tests")]
public class ObservabilityCorrelationTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    [Fact]
    public async Task TestEndpoint_ProducesTraceAndAudit_WithSameTraceId()
    {
        var client = GetHttpClient();

        // Call dedicated test endpoint (registered only in Development)
        var response = await client.GetAsync("/_tests/audit-correlation");
        response.EnsureSuccessStatusCode();

        // Inspect in-memory collectors
        using var scope = GetServiceScope();
        var audit = scope.ServiceProvider.GetRequiredService<FiniteAutomatons.Observability.InMemoryAuditService>();
        var collector = scope.ServiceProvider.GetRequiredService<FiniteAutomatons.Observability.InMemoryActivityCollector>();

        // Give some time for Activity listeners to capture
        await Task.Delay(100);

        var auditEntries = audit.GetAll();
        auditEntries.ShouldNotBeEmpty();

        var traceEntries = collector.GetAll();
        traceEntries.ShouldNotBeEmpty();

        // Find the latest audit entry with EventType 'TestEndpoint'
        var lastAudit = auditEntries.Where(a => a.EventType == "TestEndpoint").Last();
        var lastTrace = traceEntries.Last();

        var auditId = lastAudit.TraceId;
        var traceId = lastTrace.TraceId;

        auditId.ShouldNotBeNullOrEmpty();
        traceId.ShouldNotBeNullOrEmpty();
        auditId.ShouldBe(traceId);
    }
}
