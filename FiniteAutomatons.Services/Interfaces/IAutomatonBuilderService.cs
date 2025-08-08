using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.ViewModel;

namespace FiniteAutomatons.Services.Interfaces;

/// <summary>
/// Service for building automaton instances from view models
/// </summary>
public interface IAutomatonBuilderService
{
    /// <summary>
    /// Creates an automaton instance from a view model
    /// </summary>
    /// <param name="model">The automaton view model</param>
    /// <returns>The created automaton instance</returns>
    Automaton CreateAutomatonFromModel(AutomatonViewModel model);

    /// <summary>
    /// Creates a DFA instance from a view model
    /// </summary>
    /// <param name="model">The automaton view model</param>
    /// <returns>The created DFA instance</returns>
    DFA CreateDFA(AutomatonViewModel model);

    /// <summary>
    /// Creates an NFA instance from a view model
    /// </summary>
    /// <param name="model">The automaton view model</param>
    /// <returns>The created NFA instance</returns>
    NFA CreateNFA(AutomatonViewModel model);

    /// <summary>
    /// Creates an Epsilon NFA instance from a view model
    /// </summary>
    /// <param name="model">The automaton view model</param>
    /// <returns>The created Epsilon NFA instance</returns>
    EpsilonNFA CreateEpsilonNFA(AutomatonViewModel model);
}