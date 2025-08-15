using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.Extensions.Logging;
using FiniteAutomatons.Core.Utilities;

namespace FiniteAutomatons.Services.Services;

/// <summary>
/// Service for validating automaton models and business rules
/// </summary>
public class AutomatonValidationService : IAutomatonValidationService
{
    private readonly ILogger<AutomatonValidationService> _logger;

    public AutomatonValidationService(ILogger<AutomatonValidationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates an automaton model for correctness and consistency
    /// </summary>
    /// <param name="model">The automaton model to validate</param>
    /// <returns>A tuple containing validation result and list of error messages</returns>
    public (bool IsValid, List<string> Errors) ValidateAutomaton(AutomatonViewModel model)
    {
        var errors = new List<string>();

        // Ensure collections are initialized
        model.States ??= [];
        model.Transitions ??= [];

        if (model.States.Count == 0)
        {
            errors.Add("Automaton must have at least one state.");
        }

        if (!model.States.Any(s => s.IsStart))
        {
            errors.Add("Automaton must have exactly one start state.");
        }

        if (model.States.Count(s => s.IsStart) > 1)
        {
            errors.Add("Automaton must have exactly one start state.");
        }

        // Additional DFA-specific validation
        if (model.Type == AutomatonType.DFA)
        {
            // Check for determinism: no two transitions from the same state on the same symbol
            var groupedTransitions = model.Transitions
                .GroupBy(t => new { t.FromStateId, t.Symbol })
                .Where(g => g.Count() > 1)
                .ToList();

            if (groupedTransitions.Count != 0)
            {
                var conflicts = groupedTransitions.Select(g =>
                    $"State {g.Key.FromStateId} on symbol '{(g.Key.Symbol == AutomatonSymbolHelper.EpsilonInternal ? AutomatonSymbolHelper.EpsilonDisplay : g.Key.Symbol.ToString())}'");
                errors.Add($"DFA cannot have multiple transitions from the same state on the same symbol. Conflicts: {string.Join(", ", conflicts)}");
            }

            // DFA should not have epsilon transitions
            if (model.Transitions.Any(t => t.Symbol == AutomatonSymbolHelper.EpsilonInternal))
            {
                errors.Add("DFA cannot have epsilon transitions.");
            }
        }

        var isValid = errors.Count == 0;
        _logger.LogInformation("Automaton validation completed: {IsValid}, Errors: {ErrorCount}", isValid, errors.Count);

        return (isValid, errors);
    }

    /// <summary>
    /// Validates if a state can be added to the automaton
    /// </summary>
    public (bool IsValid, string? ErrorMessage) ValidateStateAddition(AutomatonViewModel model, int stateId, bool isStart)
    {
        model.States ??= [];

        if (model.States.Any(s => s.Id == stateId))
        {
            return (false, $"State with ID {stateId} already exists.");
        }

        if (isStart && model.States.Any(s => s.IsStart))
        {
            return (false, "Only one start state is allowed.");
        }

        return (true, null);
    }

    /// <summary>
    /// Validates if a transition can be added to the automaton
    /// </summary>
    public (bool IsValid, char ProcessedSymbol, string? ErrorMessage) ValidateTransitionAddition(AutomatonViewModel model, int fromStateId, int toStateId, string symbol)
    {
        model.States ??= [];
        model.Transitions ??= [];

        _logger.LogInformation("Validating transition addition: {From} -> {To} on '{Symbol}'", fromStateId, toStateId, symbol ?? "NULL");

        if (!model.States.Any(s => s.Id == fromStateId))
        {
            return (false, AutomatonSymbolHelper.EpsilonInternal, $"From state {fromStateId} does not exist.");
        }

        if (!model.States.Any(s => s.Id == toStateId))
        {
            return (false, AutomatonSymbolHelper.EpsilonInternal, $"To state {toStateId} does not exist.");
        }

        char transitionSymbol;
        if (AutomatonSymbolHelper.IsEpsilon(symbol))
        {
            _logger.LogInformation("Epsilon transition detected");
            if (model.Type != AutomatonType.EpsilonNFA)
            {
                return (false, AutomatonSymbolHelper.EpsilonInternal, $"Epsilon transitions ({AutomatonSymbolHelper.EpsilonDisplay}) are only allowed in Epsilon NFAs. Please change the automaton type or use a different symbol.");
            }
            transitionSymbol = AutomatonSymbolHelper.EpsilonInternal;
        }
        else if (!string.IsNullOrWhiteSpace(symbol) && symbol.Trim().Length == 1)
        {
            transitionSymbol = symbol.Trim()[0];
        }
        else
        {
            return (false, AutomatonSymbolHelper.EpsilonInternal, $"Symbol must be a single character or epsilon ({AutomatonSymbolHelper.EpsilonDisplay}) for Epsilon NFA.");
        }

        if (model.Type == AutomatonType.DFA &&
            model.Transitions.Any(t => t.FromStateId == fromStateId && t.Symbol == transitionSymbol))
        {
            return (false, AutomatonSymbolHelper.EpsilonInternal, $"DFA cannot have multiple transitions from state {fromStateId} on symbol '{(transitionSymbol == AutomatonSymbolHelper.EpsilonInternal ? AutomatonSymbolHelper.EpsilonDisplay : transitionSymbol.ToString())}'. ");
        }

        if (model.Transitions.Any(t => t.FromStateId == fromStateId && t.ToStateId == toStateId && t.Symbol == transitionSymbol))
        {
            return (false, AutomatonSymbolHelper.EpsilonInternal, $"Transition from {fromStateId} to {toStateId} on '{(transitionSymbol == AutomatonSymbolHelper.EpsilonInternal ? AutomatonSymbolHelper.EpsilonDisplay : transitionSymbol.ToString())}' already exists.");
        }

        return (true, transitionSymbol, null);
    }
}