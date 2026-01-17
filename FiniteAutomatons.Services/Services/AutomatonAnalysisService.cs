using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Services.Interfaces;

namespace FiniteAutomatons.Services.Services;

public class AutomatonAnalysisService : IAutomatonAnalysisService
{
    public IReadOnlyCollection<int> GetReachableStates(IEnumerable<Transition> transitions, int startId)
    {
        if (transitions == null) throw new NullReferenceException(nameof(transitions));

        var reachable = new HashSet<int>();
        var q = new Queue<int>();
        q.Enqueue(startId);
        reachable.Add(startId);
        var byFrom = transitions.GroupBy(t => t.FromStateId).ToDictionary(g => g.Key, g => g.Select(t => t.ToStateId).ToList());
        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            if (!byFrom.TryGetValue(cur, out var tos)) continue;
            foreach (var to in tos)
            {
                if (reachable.Add(to)) q.Enqueue(to);
            }
        }

        return reachable;
    }

    public int GetReachableCount(IEnumerable<Transition> transitions, int startId)
    {
        return GetReachableStates(transitions, startId).Count;
    }
}
