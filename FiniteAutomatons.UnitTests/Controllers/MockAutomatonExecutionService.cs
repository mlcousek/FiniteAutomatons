using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;

namespace FiniteAutomatons.UnitTests.Controllers;

public class MockAutomatonExecutionService : IAutomatonExecutionService
{
    public AutomatonExecutionState ReconstructState(AutomatonViewModel model)
    {
        if (model.Type == AutomatonType.DFA)
        {
            return new AutomatonExecutionState(model.Input ?? "", model.CurrentStateId)
            {
                Position = model.Position,
                IsAccepted = model.IsAccepted
            };
        }
        
        return new AutomatonExecutionState(model.Input ?? "", null, model.CurrentStates ?? [])
        {
            Position = model.Position,
            IsAccepted = model.IsAccepted
        };
    }

    public void UpdateModelFromState(AutomatonViewModel model, AutomatonExecutionState state)
    {
        model.Position = state.Position;
        model.IsAccepted = state.IsAccepted;
        
        if (model.Type == AutomatonType.DFA)
        {
            model.CurrentStateId = state.CurrentStateId;
        }
        else
        {
            model.CurrentStates = state.CurrentStates ?? [];
        }
    }

    public void EnsureProperStateInitialization(AutomatonViewModel model, Automaton automaton)
    {
        if (model.Type == AutomatonType.DFA)
        {
            model.CurrentStateId ??= model.States?.FirstOrDefault(s => s.IsStart)?.Id;
        }
        else
        {
            model.CurrentStates ??= model.States?.Where(s => s.IsStart).Select(s => s.Id).ToHashSet() ?? [];
        }
    }

    public AutomatonViewModel ExecuteStepForward(AutomatonViewModel model)
    {
        model.Position = Math.Min(model.Position + 1, model.Input?.Length ?? 0);
        return model;
    }

    public AutomatonViewModel ExecuteStepBackward(AutomatonViewModel model)
    {
        model.Position = Math.Max(model.Position - 1, 0);
        return model;
    }

    public AutomatonViewModel ExecuteAll(AutomatonViewModel model)
    {
        model.Position = model.Input?.Length ?? 0;
        model.Result = true; 
        return model;
    }

    public AutomatonViewModel BackToStart(AutomatonViewModel model)
    {
        model.Position = 0;
        model.Result = null;
        return model;
    }

    public AutomatonViewModel ResetExecution(AutomatonViewModel model)
    {
        model.States ??= [];
        model.Transitions ??= [];

        model.Input = string.Empty;
        model.Position = 0;
        model.Result = null;
        model.CurrentStateId = null;
        model.CurrentStates = null;
        model.IsAccepted = null;
        model.StateHistorySerialized = string.Empty;

        var transitionSymbols = model.Transitions
            .Where(t => t.Symbol != '\0') 
            .Select(t => t.Symbol)
            .Distinct()
            .ToList();
        
        return model;
    }
}
