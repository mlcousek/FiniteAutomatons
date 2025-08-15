using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace FiniteAutomatons.Services.Services;

/// <summary>
/// Service for validating automaton models and business rules
/// </summary>
public class AutomatonValidationService : IAutomatonValidationService
{
    private readonly ILogger<AutomatonValidationService> _logger;

    // Central list of accepted epsilon aliases (all mapped to '\0')
    private static readonly HashSet<string> _epsilonAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "", "?", "?", "epsilon", "eps", "e", "lambda", "?"
    };

    public AutomatonValidationService(ILogger<AutomatonValidationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Helper to determine if a user supplied symbol represents epsilon
    /// </summary>
    private static bool IsEpsilon(string? symbol)
        => symbol is null || _epsilonAliases.Contains(symbol.Trim());

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
                    $"State {g.Key.FromStateId} on symbol '{(g.Key.Symbol == '\0' ? "?" : g.Key.Symbol.ToString())}'");
                errors.Add($"DFA cannot have multiple transitions from the same state on the same symbol. Conflicts: {string.Join(", ", conflicts)}");
            }

            // DFA should not have epsilon transitions
            if (model.Transitions.Any(t => t.Symbol == '\0'))
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
    /// <param name="model">The current automaton model</param>
    /// <param name="stateId">The ID of the state to add</param>
    /// <param name="isStart">Whether the state is a start state</param>
    /// <returns>A tuple containing validation result and error message if any</returns>
    public (bool IsValid, string? ErrorMessage) ValidateStateAddition(AutomatonViewModel model, int stateId, bool isStart)
    {
        // Ensure collections are initialized
        model.States ??= [];

        // Check if state ID already exists
        if (model.States.Any(s => s.Id == stateId))
        {
            return (false, $"State with ID {stateId} already exists.");
        }

        // Check if trying to add another start state
        if (isStart && model.States.Any(s => s.IsStart))
        {
            return (false, "Only one start state is allowed.");
        }

        return (true, null);
    }

    /// <summary>
    /// Validates if a transition can be added to the automaton
    /// </summary>
    /// <param name="model">The current automaton model</param>
    /// <param name="fromStateId">Source state ID</param>
    /// <param name="toStateId">Target state ID</param>
    /// <param name="symbol">Transition symbol</param>
    /// <returns>A tuple containing validation result, processed symbol, and error message if any</returns>
    public (bool IsValid, char ProcessedSymbol, string? ErrorMessage) ValidateTransitionAddition(AutomatonViewModel model, int fromStateId, int toStateId, string symbol)
    {
        // Ensure collections are initialized
        model.States ??= [];
        model.Transitions ??= [];

        _logger.LogInformation("Validating transition addition: {From} -> {To} on '{Symbol}'", fromStateId, toStateId, symbol ?? "NULL");

        // Validate states exist
        if (!model.States.Any(s => s.Id == fromStateId))
        {
            return (false, '\0', $"From state {fromStateId} does not exist.");
        }

        if (!model.States.Any(s => s.Id == toStateId))
        {
            return (false, '\0', $"To state {toStateId} does not exist.");
        }

        var isEpsilonTransition = IsEpsilon(symbol);
        char transitionSymbol;

        if (isEpsilonTransition)
        {
            _logger.LogInformation("Epsilon transition detected");
            // Epsilon symbols are only allowed in Epsilon NFA
            if (model.Type != AutomatonType.EpsilonNFA)
            {
                return (false, '\0', "Epsilon transitions (?) are only allowed in Epsilon NFAs. Please change the automaton type or use a different symbol.");
            }
            transitionSymbol = '\0';
        }
        else if (!string.IsNullOrWhiteSpace(symbol) && symbol.Trim().Length == 1)
        {
            transitionSymbol = symbol.Trim()[0];
        }
        else
        {
            return (false, '\0', "Symbol must be a single character or epsilon (?) for Epsilon NFA.");
        }

        // Check if transition already exists (for DFA, this matters more)
        if (model.Type == AutomatonType.DFA &&
            model.Transitions.Any(t => t.FromStateId == fromStateId && t.Symbol == transitionSymbol))
        {
            return (false, '\0', $"DFA cannot have multiple transitions from state {fromStateId} on symbol '{(transitionSymbol == '\0' ? "?" : transitionSymbol.ToString())}'.");
        }

        // Check for exact duplicate transitions
        if (model.Transitions.Any(t => t.FromStateId == fromStateId && t.ToStateId == toStateId && t.Symbol == transitionSymbol))
        {
            return (false, '\0', $"Transition from {fromStateId} to {toStateId} on '{(transitionSymbol == '\0' ? "?" : transitionSymbol.ToString())}' already exists.");
        }

        return (true, transitionSymbol, null);
    }
}