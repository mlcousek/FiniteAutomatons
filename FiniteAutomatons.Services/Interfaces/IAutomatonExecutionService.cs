using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;

namespace FiniteAutomatons.Services.Interfaces;

public interface IAutomatonExecutionService
{
    AutomatonExecutionState ReconstructState(AutomatonViewModel model);
    void UpdateModelFromState(AutomatonViewModel model, AutomatonExecutionState state);
    void EnsureProperStateInitialization(AutomatonViewModel model, Automaton automaton);
    AutomatonViewModel ExecuteStepForward(AutomatonViewModel model);
    AutomatonViewModel ExecuteStepBackward(AutomatonViewModel model);
    AutomatonViewModel ExecuteAll(AutomatonViewModel model);
    AutomatonViewModel BackToStart(AutomatonViewModel model);
    AutomatonViewModel ResetExecution(AutomatonViewModel model);
}
