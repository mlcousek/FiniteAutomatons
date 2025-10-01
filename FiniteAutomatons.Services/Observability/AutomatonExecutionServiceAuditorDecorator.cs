using System;
using System.Threading.Tasks;
using FiniteAutomatons.Observability;
using FiniteAutomatons.Services.Interfaces;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Services.Services;

namespace FiniteAutomatons.Services.Observability;

public sealed class AutomatonExecutionServiceAuditorDecorator : IAutomatonExecutionService
{
    private readonly AutomatonExecutionService _inner;
    private readonly IAuditService _audit;

    public AutomatonExecutionServiceAuditorDecorator(AutomatonExecutionService inner, IAuditService audit)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    public AutomatonExecutionState ReconstructState(AutomatonViewModel model)
    {
        return MethodAuditor.AuditAsync(_audit, "IAutomatonExecutionService.ReconstructState", () => Task.FromResult(_inner.ReconstructState(model))).GetAwaiter().GetResult();
    }

    public void UpdateModelFromState(AutomatonViewModel model, AutomatonExecutionState state)
    {
        MethodAuditor.AuditAsync(_audit, "IAutomatonExecutionService.UpdateModelFromState", () =>
        {
            _inner.UpdateModelFromState(model, state);
            return Task.CompletedTask;
        }).GetAwaiter().GetResult();
    }

    public void EnsureProperStateInitialization(AutomatonViewModel model, Automaton automaton)
    {
        MethodAuditor.AuditAsync(_audit, "IAutomatonExecutionService.EnsureProperStateInitialization", () =>
        {
            _inner.EnsureProperStateInitialization(model, automaton);
            return Task.CompletedTask;
        }).GetAwaiter().GetResult();
    }

    public AutomatonViewModel ExecuteStepForward(AutomatonViewModel model)
    {
        return MethodAuditor.AuditAsync(_audit, "IAutomatonExecutionService.ExecuteStepForward", () => Task.FromResult(_inner.ExecuteStepForward(model))).GetAwaiter().GetResult();
    }

    public AutomatonViewModel ExecuteStepBackward(AutomatonViewModel model)
    {
        return MethodAuditor.AuditAsync(_audit, "IAutomatonExecutionService.ExecuteStepBackward", () => Task.FromResult(_inner.ExecuteStepBackward(model))).GetAwaiter().GetResult();
    }

    public AutomatonViewModel ExecuteAll(AutomatonViewModel model)
    {
        return MethodAuditor.AuditAsync(_audit, "IAutomatonExecutionService.ExecuteAll", () => Task.FromResult(_inner.ExecuteAll(model))).GetAwaiter().GetResult();
    }

    public AutomatonViewModel BackToStart(AutomatonViewModel model)
    {
        return MethodAuditor.AuditAsync(_audit, "IAutomatonExecutionService.BackToStart", () => Task.FromResult(_inner.BackToStart(model))).GetAwaiter().GetResult();
    }

    public AutomatonViewModel ResetExecution(AutomatonViewModel model)
    {
        return MethodAuditor.AuditAsync(_audit, "IAutomatonExecutionService.ResetExecution", () => Task.FromResult(_inner.ResetExecution(model))).GetAwaiter().GetResult();
    }
}
