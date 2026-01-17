using FiniteAutomatons.Core.Models.ViewModel;

namespace FiniteAutomatons.Services.Interfaces;

public interface IAutomatonMinimizationService
{
    (AutomatonViewModel Result, string Message) MinimizeDfa(AutomatonViewModel model);
    MinimizationAnalysis AnalyzeAutomaton(AutomatonViewModel model);
}

public sealed record MinimizationAnalysis(bool SupportsMinimization, bool IsMinimal, int OriginalStateCount, int ReachableStateCount, int MinimizedStateCount);
