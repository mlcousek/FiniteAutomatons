using FiniteAutomatons.Observability;
using FiniteAutomatons.Services.Interfaces;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Services.Services;

namespace FiniteAutomatons.Services.Observability;

public sealed class AutomatonExecutionServiceAuditorDecorator(AutomatonExecutionService inner, IAuditService audit) : IAutomatonExecutionService
{
    private readonly AutomatonExecutionService inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly IAuditService audit = audit ?? throw new ArgumentNullException(nameof(audit));

    public AutomatonExecutionState ReconstructState(AutomatonViewModel model)
    {
        return MethodAuditor.AuditAsync(audit, "IAutomatonExecutionService.ReconstructState", () => Task.FromResult(inner.ReconstructState(model))).GetAwaiter().GetResult();
    }

    public void UpdateModelFromState(AutomatonViewModel model, AutomatonExecutionState state)
    {
        MethodAuditor.AuditAsync(audit, "IAutomatonExecutionService.UpdateModelFromState", () =>
        {
            inner.UpdateModelFromState(model, state);
            return Task.CompletedTask;
        }).GetAwaiter().GetResult();
    }

    public void EnsureProperStateInitialization(AutomatonViewModel model, Automaton automaton)
    {
        MethodAuditor.AuditAsync(audit, "IAutomatonExecutionService.EnsureProperStateInitialization", () =>
        {
            inner.EnsureProperStateInitialization(model, automaton);
            return Task.CompletedTask;
        }).GetAwaiter().GetResult();
    }

    public AutomatonViewModel ExecuteStepForward(AutomatonViewModel model)
    {
        return MethodAuditor.AuditAsync(audit, "IAutomatonExecutionService.ExecuteStepForward", () => Task.FromResult(inner.ExecuteStepForward(model))).GetAwaiter().GetResult();
    }

    public AutomatonViewModel ExecuteStepBackward(AutomatonViewModel model)
    {
        return MethodAuditor.AuditAsync(audit, "IAutomatonExecutionService.ExecuteStepBackward", () => Task.FromResult(inner.ExecuteStepBackward(model))).GetAwaiter().GetResult();
    }

    public AutomatonViewModel ExecuteAll(AutomatonViewModel model)
    {
        return MethodAuditor.AuditAsync(audit, "IAutomatonExecutionService.ExecuteAll", () => Task.FromResult(inner.ExecuteAll(model))).GetAwaiter().GetResult();
    }

    public AutomatonViewModel BackToStart(AutomatonViewModel model)
    {
        return MethodAuditor.AuditAsync(audit, "IAutomatonExecutionService.BackToStart", () => Task.FromResult(inner.BackToStart(model))).GetAwaiter().GetResult();
    }

    public AutomatonViewModel ResetExecution(AutomatonViewModel model)
    {
        return MethodAuditor.AuditAsync(audit, "IAutomatonExecutionService.ResetExecution", () => Task.FromResult(inner.ResetExecution(model))).GetAwaiter().GetResult();
    }
}
