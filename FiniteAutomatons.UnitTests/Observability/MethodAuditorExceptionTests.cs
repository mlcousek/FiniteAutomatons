using System;
using System.Threading.Tasks;
using FiniteAutomatons.Observability;
using Shouldly;
using Xunit;

namespace FiniteAutomatons.UnitTests.Observability;

public class MethodAuditorExceptionTests
{
    [Fact]
    public async Task AuditAsync_WhenFunctionThrows_EmitsMethodError()
    {
        var audit = new InMemoryAuditService();

        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await MethodAuditor.AuditAsync(audit, "FailingMethod", async () =>
            {
                await Task.Delay(1);
                throw new InvalidOperationException("boom");
            });
        });

        var error = audit.GetByEventType("MethodError").FirstOrDefault(r => r.Message == "FailingMethod");
        error.ShouldNotBeNull();
        error.Data.ShouldNotBeNull();
        error.Data.ContainsKey("Exception").ShouldBeTrue();
        error.Data.ContainsKey("DurationMs").ShouldBeTrue();
    }
}
