using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Core.Utilities;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace FiniteAutomatons.Services.Services;

public class AutomatonValidationService(ILogger<AutomatonValidationService> logger) : IAutomatonValidationService
{
    private readonly ILogger<AutomatonValidationService> logger = logger;

    public (bool IsValid, List<string> Errors) ValidateAutomaton(AutomatonViewModel model)
    {
        var errors = new List<string>();

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

        if (model.Type == AutomatonType.DFA)
        {
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

            if (model.Transitions.Any(t => t.Symbol == AutomatonSymbolHelper.EpsilonInternal))
            {
                errors.Add("DFA cannot have epsilon transitions.");
            }
        }

        if (model.Type == AutomatonType.PDA)
        {
            var grouped = model.Transitions
                .GroupBy(t => new { t.FromStateId, Symbol = t.Symbol, Stack = t.StackPop ?? '\0' })
                .Where(g => g.Count() > 1)
                .ToList();
            if (grouped.Count > 0)
            {
                errors.Add("PDA must be deterministic: multiple transitions with same (state, input symbol, stack pop) detected.");
            }
        }

        var isValid = errors.Count == 0;
        logger.LogInformation("Automaton validation completed: {IsValid}, Errors: {ErrorCount}", isValid, errors.Count);

        return (isValid, errors);
    }

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

    public (bool IsValid, char ProcessedSymbol, string? ErrorMessage) ValidateTransitionAddition(AutomatonViewModel model, int fromStateId, int toStateId, string symbol)
    {
        model.States ??= [];
        model.Transitions ??= [];

        logger.LogInformation("Validating transition addition: {From} -> {To} on '{Symbol}'", fromStateId, toStateId, symbol ?? "NULL");

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
            logger.LogInformation("Epsilon transition detected");
            if (model.Type != AutomatonType.EpsilonNFA && model.Type != AutomatonType.PDA)
            {
                return (false, AutomatonSymbolHelper.EpsilonInternal, $"Epsilon transitions (ε) are only allowed in Epsilon NFAs or PDAs. Please change the automaton type or use a different symbol.");
            }
            transitionSymbol = AutomatonSymbolHelper.EpsilonInternal;
        }
        else if (!string.IsNullOrWhiteSpace(symbol) && symbol.Trim().Length == 1)
        {
            transitionSymbol = symbol.Trim()[0];
        }
        else
        {
            return (false, AutomatonSymbolHelper.EpsilonInternal, $"Symbol must be a single character or epsilon ({AutomatonSymbolHelper.EpsilonDisplay}) for Epsilon NFA / PDA.");
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
