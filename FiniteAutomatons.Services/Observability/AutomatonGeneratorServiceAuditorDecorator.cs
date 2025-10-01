using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using FiniteAutomatons.Observability;
using FiniteAutomatons.Services.Interfaces;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Services.Services;

namespace FiniteAutomatons.Services.Observability;

public sealed class AutomatonGeneratorServiceAuditorDecorator : IAutomatonGeneratorService
{
    private readonly AutomatonGeneratorService _inner;
    private readonly IAuditService _audit;

    public AutomatonGeneratorServiceAuditorDecorator(AutomatonGeneratorService inner, IAuditService audit)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public AutomatonViewModel GenerateRandomAutomaton(AutomatonType type, int stateCount, int transitionCount, int alphabetSize = 3, double acceptingStateRatio = 0.3, int? seed = null)
    {
        return MethodAuditor.AuditAsync(_audit, "IAutomatonGeneratorService.GenerateRandomAutomaton", () =>
            Task.FromResult(_inner.GenerateRandomAutomaton(type, stateCount, transitionCount, alphabetSize, acceptingStateRatio, seed)))
            .GetAwaiter().GetResult();
    }

    public AutomatonViewModel GenerateRealisticAutomaton(AutomatonType type, int stateCount, int? seed = null)
    {
        return MethodAuditor.AuditAsync(_audit, "IAutomatonGeneratorService.GenerateRealisticAutomaton", () =>
            Task.FromResult(_inner.GenerateRealisticAutomaton(type, stateCount, seed)))
            .GetAwaiter().GetResult();
    }

    public bool ValidateGenerationParameters(AutomatonType type, int stateCount, int transitionCount, int alphabetSize)
    {
        return MethodAuditor.AuditAsync(_audit, "IAutomatonGeneratorService.ValidateGenerationParameters", () =>
            Task.FromResult(_inner.ValidateGenerationParameters(type, stateCount, transitionCount, alphabetSize)))
            .GetAwaiter().GetResult();
    }
}
