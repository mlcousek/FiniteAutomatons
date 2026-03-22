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

        ValidateBasicStructure(model, errors);
        ValidateTypeSpecificRules(model, errors);

        var isValid = errors.Count == 0;
        LogValidationResult(isValid, errors.Count);

        return (isValid, errors);
    }

    private static void ValidateBasicStructure(AutomatonViewModel model, List<string> errors)
    {
        ValidateStateCount(model, errors);
        ValidateStartState(model, errors);
    }

    private static void ValidateStateCount(AutomatonViewModel model, List<string> errors)
    {
        if (model.States.Count == 0)
        {
            errors.Add("Automaton must have at least one state.");
        }
    }

    private static void ValidateStartState(AutomatonViewModel model, List<string> errors)
    {
        var startStateCount = model.States.Count(s => s.IsStart);

        if (startStateCount == 0)
        {
            errors.Add("Automaton must have exactly one start state.");
        }
        else if (startStateCount > 1)
        {
            errors.Add("Automaton must have exactly one start state.");
        }
    }

    private static void ValidateTypeSpecificRules(AutomatonViewModel model, List<string> errors)
    {
        switch (model.Type)
        {
            case AutomatonType.DFA:
                ValidateDfaRules(model, errors);
                break;
            case AutomatonType.DPDA:
                ValidateDpdaRules(model, errors);
                break;
            case AutomatonType.NPDA:
                ValidateNpdaRules(model, errors);
                break;
        }
    }

    private static void ValidateDfaRules(AutomatonViewModel model, List<string> errors)
    {
        ValidateDfaDeterminism(model, errors);
        ValidateDfaNoEpsilonTransitions(model, errors);
    }

    private static void ValidateDfaDeterminism(AutomatonViewModel model, List<string> errors)
    {
        var groupedTransitions = model.Transitions
            .GroupBy(t => new { t.FromStateId, t.Symbol })
            .Where(g => g.Count() > 1)
            .ToList();

        if (groupedTransitions.Count == 0)
            return;

        var conflicts = groupedTransitions.Select(g =>
            $"State {g.Key.FromStateId} on symbol '{FormatSymbol(g.Key.Symbol)}'");

        errors.Add($"DFA cannot have multiple transitions from the same state on the same symbol. Conflicts: {string.Join(", ", conflicts)}");
    }

    private static void ValidateDfaNoEpsilonTransitions(AutomatonViewModel model, List<string> errors)
    {
        if (model.Transitions.Any(t => t.Symbol == AutomatonSymbolHelper.EpsilonInternal))
        {
            errors.Add("DFA cannot have epsilon transitions.");
        }
    }

    private static void ValidateDpdaRules(AutomatonViewModel model, List<string> errors)
    {
        var grouped = model.Transitions
            .GroupBy(t => new { t.FromStateId, t.Symbol, Stack = t.StackPop ?? '\0' })
            .Where(g => g.Count() > 1)
            .ToList();

        if (grouped.Count > 0)
        {
            errors.Add("DPDA must be deterministic: multiple transitions with same (state, input symbol, stack pop) detected.");
        }

        var epsilonTransitions = model.Transitions.Where(t => t.Symbol == AutomatonSymbolHelper.EpsilonInternal);
        foreach (var eps in epsilonTransitions)
        {
            var conflicts = model.Transitions.Where(t =>
                t.FromStateId == eps.FromStateId &&
                t.Symbol != AutomatonSymbolHelper.EpsilonInternal &&
                (t.StackPop ?? '\0') == (eps.StackPop ?? '\0'));

            if (conflicts.Any())
            {
                errors.Add($"DPDA determinism conflict: State {eps.FromStateId} has both an epsilon transition and symbol transitions for stack pop '{eps.StackPop ?? '\0'}'.");
            }
        }
    }

    private static void ValidateNpdaRules(AutomatonViewModel model, List<string> errors)
    {
        // NPDA allows nondeterminism, so we only check for identical duplicate transitions
        // which is already handled by CheckDuplicateTransition during addition.
    }

    private static string FormatSymbol(char symbol)
    {
        return symbol == AutomatonSymbolHelper.EpsilonInternal
            ? AutomatonSymbolHelper.EpsilonDisplay.ToString()
            : symbol.ToString();
    }

    private void LogValidationResult(bool isValid, int errorCount)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Automaton validation completed: {IsValid}, Errors: {ErrorCount}", isValid, errorCount);
        }
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

        LogTransitionValidationStart(fromStateId, toStateId, symbol);

        var stateValidation = ValidateStatesExist(model, fromStateId, toStateId);
        if (!stateValidation.IsValid)
            return stateValidation;

        var symbolValidation = ParseAndValidateSymbol(model, symbol);
        if (!symbolValidation.IsValid)
            return symbolValidation;

        var conflictValidation = CheckTransitionConflicts(model, fromStateId, toStateId, symbolValidation.ProcessedSymbol);
        if (!conflictValidation.IsValid)
            return conflictValidation;

        return (true, symbolValidation.ProcessedSymbol, null);
    }

    private static (bool IsValid, char ProcessedSymbol, string? ErrorMessage) ValidateStatesExist(AutomatonViewModel model, int fromStateId, int toStateId)
    {
        if (!model.States.Any(s => s.Id == fromStateId))
        {
            return (false, AutomatonSymbolHelper.EpsilonInternal, $"From state {fromStateId} does not exist.");
        }

        if (!model.States.Any(s => s.Id == toStateId))
        {
            return (false, AutomatonSymbolHelper.EpsilonInternal, $"To state {toStateId} does not exist.");
        }

        return (true, AutomatonSymbolHelper.EpsilonInternal, null);
    }

    private (bool IsValid, char ProcessedSymbol, string? ErrorMessage) ParseAndValidateSymbol(AutomatonViewModel model, string symbol)
    {
        if (AutomatonSymbolHelper.IsEpsilon(symbol))
        {
            return ValidateEpsilonTransition(model);
        }

        if (!string.IsNullOrWhiteSpace(symbol) && symbol.Trim().Length == 1)
        {
            return (true, symbol.Trim()[0], null);
        }

        return (false, AutomatonSymbolHelper.EpsilonInternal,
            $"Symbol must be a single character or epsilon ({AutomatonSymbolHelper.EpsilonDisplay}) for Epsilon NFA / PDA.");
    }

    private (bool IsValid, char ProcessedSymbol, string? ErrorMessage) ValidateEpsilonTransition(AutomatonViewModel model)
    {
        logger.LogInformation("Epsilon transition detected");

        if (model.Type != AutomatonType.EpsilonNFA && model.Type != AutomatonType.DPDA && model.Type != AutomatonType.NPDA)
        {
            return (false, AutomatonSymbolHelper.EpsilonInternal,
                $"Epsilon transitions (ε) are only allowed in Epsilon NFAs, DPDAs, or NPDAs. Please change the automaton type or use a different symbol.");
        }

        return (true, AutomatonSymbolHelper.EpsilonInternal, null);
    }

    private static (bool IsValid, char ProcessedSymbol, string? ErrorMessage) CheckTransitionConflicts(AutomatonViewModel model, int fromStateId, int toStateId, char symbol)
    {
        var dfaConflict = CheckDfaConflict(model, fromStateId, symbol);
        if (!dfaConflict.IsValid)
            return dfaConflict;

        var duplicateCheck = CheckDuplicateTransition(model, fromStateId, toStateId, symbol);
        if (!duplicateCheck.IsValid)
            return duplicateCheck;

        return (true, symbol, null);
    }

    private static (bool IsValid, char ProcessedSymbol, string? ErrorMessage) CheckDfaConflict(AutomatonViewModel model, int fromStateId, char symbol)
    {
        if (model.Type != AutomatonType.DFA)
            return (true, symbol, null);

        if (!model.Transitions.Any(t => t.FromStateId == fromStateId && t.Symbol == symbol))
            return (true, symbol, null);

        return (false, AutomatonSymbolHelper.EpsilonInternal,
            $"DFA cannot have multiple transitions from state {fromStateId} on symbol '{FormatSymbol(symbol)}'. ");
    }

    private static (bool IsValid, char ProcessedSymbol, string? ErrorMessage) CheckDuplicateTransition(AutomatonViewModel model, int fromStateId, int toStateId, char symbol)
    {
        if (!model.Transitions.Any(t => t.FromStateId == fromStateId && t.ToStateId == toStateId && t.Symbol == symbol))
            return (true, symbol, null);

        return (false, AutomatonSymbolHelper.EpsilonInternal,
            $"Transition from {fromStateId} to {toStateId} on '{FormatSymbol(symbol)}' already exists.");
    }

    private void LogTransitionValidationStart(int fromStateId, int toStateId, string? symbol)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Validating transition addition: {From} -> {To} on '{Symbol}'",
                fromStateId, toStateId, symbol ?? "NULL");
        }
    }
}
