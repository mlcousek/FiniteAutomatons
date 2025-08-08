using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;

namespace FiniteAutomatons.Services.Interfaces;

/// <summary>
/// Service for handling automaton execution operations
/// </summary>
public interface IAutomatonExecutionService
{
    /// <summary>
    /// Reconstructs execution state from a view model
    /// </summary>
    /// <param name="model">The automaton view model</param>
    /// <returns>The reconstructed execution state</returns>
    AutomatonExecutionState ReconstructState(AutomatonViewModel model);

    /// <summary>
    /// Updates a view model from execution state
    /// </summary>
    /// <param name="model">The automaton view model to update</param>
    /// <param name="state">The current execution state</param>
    void UpdateModelFromState(AutomatonViewModel model, AutomatonExecutionState state);

    /// <summary>
    /// Ensures proper state initialization for an automaton
    /// </summary>
    /// <param name="model">The automaton view model</param>
    /// <param name="automaton">The automaton instance</param>
    void EnsureProperStateInitialization(AutomatonViewModel model, Automaton automaton);

    /// <summary>
    /// Executes a single step forward in the automaton
    /// </summary>
    /// <param name="model">The automaton view model</param>
    /// <returns>The updated model after step execution</returns>
    AutomatonViewModel ExecuteStepForward(AutomatonViewModel model);

    /// <summary>
    /// Executes a single step backward in the automaton
    /// </summary>
    /// <param name="model">The automaton view model</param>
    /// <returns>The updated model after step execution</returns>
    AutomatonViewModel ExecuteStepBackward(AutomatonViewModel model);

    /// <summary>
    /// Executes the automaton to completion
    /// </summary>
    /// <param name="model">The automaton view model</param>
    /// <returns>The updated model after full execution</returns>
    AutomatonViewModel ExecuteAll(AutomatonViewModel model);

    /// <summary>
    /// Resets the automaton to the start state
    /// </summary>
    /// <param name="model">The automaton view model</param>
    /// <returns>The updated model after reset</returns>
    AutomatonViewModel BackToStart(AutomatonViewModel model);

    /// <summary>
    /// Resets the execution state while preserving automaton structure
    /// </summary>
    /// <param name="model">The automaton view model</param>
    /// <returns>The updated model after reset</returns>
    AutomatonViewModel ResetExecution(AutomatonViewModel model);
}