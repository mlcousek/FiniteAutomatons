using FiniteAutomatons.Core.Models.ViewModel;

namespace FiniteAutomatons.Services.Interfaces;

public interface IAutomatonMinimizationService
{
    (AutomatonViewModel Result, string Message) MinimizeDfa(AutomatonViewModel model);
    DfaMinimizationAnalysis AnalyzeDfa(AutomatonViewModel model);
}

public sealed record DfaMinimizationAnalysis(bool SupportsMinimization, bool IsMinimal, int OriginalStateCount, int ReachableStateCount, int MinimizedStateCount);
