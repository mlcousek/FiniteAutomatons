using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Core.Utilities;

namespace FiniteAutomatons.Services.Services;

public static class DeterminismValidationHelper
{
    public static string? GetDeterminismError(AutomatonViewModel model)
    {
        model.EnsureInitialized();

        return model.Type switch
        {
            AutomatonType.DFA => GetDfaDeterminismError(model),
            AutomatonType.DPDA => GetDpdaDeterminismError(model),
            _ => null
        };
    }

    private static string? GetDfaDeterminismError(AutomatonViewModel model)
    {
        if (model.Transitions.Any(t => t.Symbol == AutomatonSymbolHelper.EpsilonInternal))
        {
            return "DFA cannot contain epsilon transitions.";
        }

        var duplicate = model.Transitions
            .GroupBy(t => new { t.FromStateId, t.Symbol })
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicate is null)
        {
            return null;
        }

        return $"DFA must be deterministic: state {duplicate.Key.FromStateId} has multiple transitions on symbol '{FormatSymbol(duplicate.Key.Symbol)}'.";
    }

    private static string? GetDpdaDeterminismError(AutomatonViewModel model)
    {
        for (int i = 0; i < model.Transitions.Count; i++)
        {
            var first = model.Transitions[i];

            for (int j = i + 1; j < model.Transitions.Count; j++)
            {
                var second = model.Transitions[j];

                if (first.FromStateId != second.FromStateId)
                {
                    continue;
                }

                if (!StackConditionsOverlap(first.StackPop, second.StackPop))
                {
                    continue;
                }

                if (first.Symbol == second.Symbol)
                {
                    return $"DPDA must be deterministic: state {first.FromStateId} has conflicting transitions on symbol '{FormatSymbol(first.Symbol)}' with overlapping stack conditions ({FormatStackPop(first.StackPop)} / {FormatStackPop(second.StackPop)}).";
                }

                var firstIsEpsilon = first.Symbol == AutomatonSymbolHelper.EpsilonInternal;
                var secondIsEpsilon = second.Symbol == AutomatonSymbolHelper.EpsilonInternal;
                if (firstIsEpsilon != secondIsEpsilon)
                {
                    return $"DPDA must be deterministic: state {first.FromStateId} cannot have epsilon and consuming transitions with overlapping stack conditions ({FormatStackPop(first.StackPop)} / {FormatStackPop(second.StackPop)}).";
                }
            }
        }

        return null;
    }

    private static bool StackConditionsOverlap(char? firstStackPop, char? secondStackPop)
    {
        var firstAny = !firstStackPop.HasValue || firstStackPop.Value == AutomatonSymbolHelper.EpsilonInternal;
        var secondAny = !secondStackPop.HasValue || secondStackPop.Value == AutomatonSymbolHelper.EpsilonInternal;

        return firstAny || secondAny || firstStackPop == secondStackPop;
    }

    private static string FormatSymbol(char symbol)
        => symbol == AutomatonSymbolHelper.EpsilonInternal
            ? AutomatonSymbolHelper.EpsilonDisplay.ToString()
            : symbol.ToString();

    private static string FormatStackPop(char? stackPop)
        => !stackPop.HasValue || stackPop.Value == AutomatonSymbolHelper.EpsilonInternal
            ? "any"
            : stackPop.Value.ToString();
}