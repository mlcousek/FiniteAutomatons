using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Observability;
using FiniteAutomatons.Services.Interfaces;
using FiniteAutomatons.Services.Services;

namespace FiniteAutomatons.Services.Observability;

public sealed class AutomatonGeneratorServiceAuditorDecorator(AutomatonGeneratorService inner, IAuditService audit) : IAutomatonGeneratorService
{
    private readonly AutomatonGeneratorService inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly IAuditService audit = audit ?? throw new ArgumentNullException(nameof(audit));

    public AutomatonViewModel GenerateRandomAutomaton(AutomatonType type, int stateCount, int transitionCount, int alphabetSize = 3, double acceptingStateRatio = 0.3, int? seed = null, PDAAcceptanceMode? acceptanceMode = null, Stack<char>? initialStack = null)
    {
        return MethodAuditor.AuditAsync(audit, "IAutomatonGeneratorService.GenerateRandomAutomaton", () =>
            Task.FromResult(inner.GenerateRandomAutomaton(type, stateCount, transitionCount, alphabetSize, acceptingStateRatio, seed, acceptanceMode, initialStack)))
            .GetAwaiter().GetResult();
    }

    public bool ValidateGenerationParameters(AutomatonType type, int stateCount, int transitionCount, int alphabetSize)
    {
        return MethodAuditor.AuditAsync(audit, "IAutomatonGeneratorService.ValidateGenerationParameters", () =>
            Task.FromResult(inner.ValidateGenerationParameters(type, stateCount, transitionCount, alphabetSize)))
            .GetAwaiter().GetResult();
    }

    public (int stateCount, int transitionCount, int alphabetSize, double acceptingRatio) GenerateRandomParameters(int? seed = null)
    {
        return MethodAuditor.AuditAsync(audit, "IAutomatonGeneratorService.GenerateRandomParameters", () =>
            Task.FromResult(inner.GenerateRandomParameters(seed)))
            .GetAwaiter().GetResult();
    }
}
