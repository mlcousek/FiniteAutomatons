using FiniteAutomatons.Core.Models.DoMain;

namespace FiniteAutomatons.Services.Interfaces;

public interface IAutomatonAnalysisService
{
    IReadOnlyCollection<int> GetReachableStates(IEnumerable<Transition> transitions, int startId);

    int GetReachableCount(IEnumerable<Transition> transitions, int startId);
}
