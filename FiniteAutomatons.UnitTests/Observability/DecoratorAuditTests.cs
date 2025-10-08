using FiniteAutomatons.Observability;
using FiniteAutomatons.Services.Observability;
using FiniteAutomatons.Services.Services;
using FiniteAutomatons.Core.Models.ViewModel;
using Shouldly;

namespace FiniteAutomatons.UnitTests.Observability;

public class DecoratorAuditTests
{
    [Fact]
    public async Task MethodAuditor_AuditAsync_RecordsStartAndEnd()
    {
        var audit = new InMemoryAuditService();

        var result = await MethodAuditor.AuditAsync(audit, "TestMethod", async () =>
        {
            await Task.Delay(1);
            return 42;
        });

        result.ShouldBe(42);

        var start = audit.GetByEventType("MethodStart").FirstOrDefault(r => r.Message == "TestMethod");
        var end = audit.GetByEventType("MethodEnd").FirstOrDefault(r => r.Message == "TestMethod");

        start.ShouldNotBeNull();
        end.ShouldNotBeNull();
        end.Data.ShouldNotBeNull();
        end.Data.ContainsKey("DurationMs").ShouldBeTrue();
    }

    [Fact]
    public void GeneratorDecorator_ValidateGenerationParameters_EmitsAudit()
    {
        var audit = new InMemoryAuditService();
        var inner = new AutomatonGeneratorService();
        var decorator = new AutomatonGeneratorServiceAuditorDecorator(inner, audit);

        var ok = decorator.ValidateGenerationParameters(AutomatonType.DFA, 5, 8, 3);

        ok.ShouldBeTrue();

        var start = audit.GetByEventType("MethodStart").FirstOrDefault(r => r.Message == "IAutomatonGeneratorService.ValidateGenerationParameters");
        var end = audit.GetByEventType("MethodEnd").FirstOrDefault(r => r.Message == "IAutomatonGeneratorService.ValidateGenerationParameters");

        start.ShouldNotBeNull();
        end.ShouldNotBeNull();
        end.Data.ShouldNotBeNull();
        end.Data.ContainsKey("DurationMs").ShouldBeTrue();
    }
}
