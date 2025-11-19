using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;

namespace FiniteAutomatons.UnitTests.Controllers;

public class MockAutomatonMinimizationService : IAutomatonMinimizationService
{
    public (AutomatonViewModel Result, string Message) MinimizeDfa(AutomatonViewModel model)
    {
        return (model, "Mock minimization executed.");
    }

    public DfaMinimizationAnalysis AnalyzeDfa(AutomatonViewModel model)
    {
        // Simple deterministic mock: mark DFA with >1 states as not minimal if any duplicate accepting flags
        var supports = model.Type == AutomatonType.DFA;
        if (!supports) return new DfaMinimizationAnalysis(false, false, model.States.Count, model.States.Count, model.States.Count);
        bool duplicateAccepting = model.States.Count(s => s.IsAccepting) > 1;
        bool isMinimal = !duplicateAccepting;
        return new DfaMinimizationAnalysis(true, isMinimal, model.States.Count, model.States.Count, isMinimal ? model.States.Count : model.States.Count - 1);
    }
}
