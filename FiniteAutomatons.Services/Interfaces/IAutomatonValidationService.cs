using FiniteAutomatons.Core.Models.ViewModel;

namespace FiniteAutomatons.Services.Interfaces;

/// <summary>
/// Service for validating automaton models and business rules
/// </summary>
public interface IAutomatonValidationService
{
    /// <summary>
    /// Validates an automaton model for correctness and consistency
    /// </summary>
    /// <param name="model">The automaton model to validate</param>
    /// <returns>A tuple containing validation result and list of error messages</returns>
    (bool IsValid, List<string> Errors) ValidateAutomaton(AutomatonViewModel model);

    /// <summary>
    /// Validates if a state can be added to the automaton
    /// </summary>
    /// <param name="model">The current automaton model</param>
    /// <param name="stateId">The ID of the state to add</param>
    /// <param name="isStart">Whether the state is a start state</param>
    /// <returns>A tuple containing validation result and error message if any</returns>
    (bool IsValid, string? ErrorMessage) ValidateStateAddition(AutomatonViewModel model, int stateId, bool isStart);

    /// <summary>
    /// Validates if a transition can be added to the automaton
    /// </summary>
    /// <param name="model">The current automaton model</param>
    /// <param name="fromStateId">Source state ID</param>
    /// <param name="toStateId">Target state ID</param>
    /// <param name="symbol">Transition symbol</param>
    /// <returns>A tuple containing validation result, processed symbol, and error message if any</returns>
    (bool IsValid, char ProcessedSymbol, string? ErrorMessage) ValidateTransitionAddition(AutomatonViewModel model, int fromStateId, int toStateId, string symbol);
}