using FiniteAutomatons.Core.Models.ViewModel;

namespace FiniteAutomatons.Core.Utilities;

public static class AutomatonViewModelExtensions
{
    public static void EnsureInitialized(this AutomatonViewModel model)
    {
        if (model == null) return;
        model.States ??= [];
        model.Transitions ??= [];
    }

    public static void ClearExecutionState(this AutomatonViewModel model, bool keepInput = false)
    {
        if (model == null) return;
        if (!keepInput) model.Input = string.Empty;
        model.Result = null;
        model.CurrentStateId = null;
        model.CurrentStates = null;
        model.Position = 0;
        model.IsAccepted = null;
        model.StateHistorySerialized = string.Empty;
        model.HasExecuted = false;
    }

    public static void NormalizeEpsilonTransitions(this AutomatonViewModel model)
    {
        if (model?.Transitions == null) return;
        foreach (var t in model.Transitions)
        {
            if (t.Symbol == AutomatonSymbolHelper.EpsilonInternal || AutomatonSymbolHelper.IsEpsilon(t.Symbol.ToString()))
            {
                t.Symbol = AutomatonSymbolHelper.EpsilonInternal;
            }
        }
    }
}
