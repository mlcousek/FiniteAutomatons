using FiniteAutomatons.Observability;
using FiniteAutomatons.Services.Interfaces;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Services;

namespace FiniteAutomatons.Services.Observability;

public sealed class AutomatonGeneratorServiceAuditorDecorator(AutomatonGeneratorService inner, IAuditService audit) : IAutomatonGeneratorService
{
    private readonly AutomatonGeneratorService inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly IAuditService audit = audit ?? throw new ArgumentNullException(nameof(audit));

    public AutomatonViewModel GenerateRandomAutomaton(AutomatonType type, int stateCount, int transitionCount, int alphabetSize = 3, double acceptingStateRatio = 0.3, int? seed = null)
    {
        return MethodAuditor.AuditAsync(audit, "IAutomatonGeneratorService.GenerateRandomAutomaton", () =>
            Task.FromResult(inner.GenerateRandomAutomaton(type, stateCount, transitionCount, alphabetSize, acceptingStateRatio, seed)))
            .GetAwaiter().GetResult();
    }

    public AutomatonViewModel GenerateRealisticAutomaton(AutomatonType type, int stateCount, int? seed = null)
    {
        return MethodAuditor.AuditAsync(audit, "IAutomatonGeneratorService.GenerateRealisticAutomaton", () =>
            Task.FromResult(inner.GenerateRealisticAutomaton(type, stateCount, seed)))
            .GetAwaiter().GetResult();
    }

    public bool ValidateGenerationParameters(AutomatonType type, int stateCount, int transitionCount, int alphabetSize)
    {
        return MethodAuditor.AuditAsync(audit, "IAutomatonGeneratorService.ValidateGenerationParameters", () =>
            Task.FromResult(inner.ValidateGenerationParameters(type, stateCount, transitionCount, alphabetSize)))
            .GetAwaiter().GetResult();
    }
}
