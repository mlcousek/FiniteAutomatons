using System.Collections.Generic;
using System.Linq;

namespace FiniteAutomatons.Core.Models.DoMain
{
    public static class DfaMinimizer
    {
        public static DFA Minimize(DFA dfa)
        {
            // Simple partitioning minimization (Hopcroft's algorithm can be used for production)
            var states = dfa.States;
            var transitions = dfa.Transitions;
            var accepting = new HashSet<int>(states.Where(s => s.IsAccepting).Select(s => s.Id));
            var nonAccepting = new HashSet<int>(states.Where(s => !s.IsAccepting).Select(s => s.Id));
            var partitions = new List<HashSet<int>> { accepting, nonAccepting };
            var alphabet = transitions.Select(t => t.Symbol).Distinct().ToList();

            bool changed;
            do
            {
                changed = false;
                var newPartitions = new List<HashSet<int>>();
                foreach (var group in partitions)
                {
                    var subGroups = group.GroupBy(stateId =>
                        string.Join(",", alphabet.Select(a =>
                            partitions.FindIndex(p => p.Contains(transitions.FirstOrDefault(t => t.FromStateId == stateId && t.Symbol == a)?.ToStateId ?? -1))
                        ))
                    ).Select(g => g.ToHashSet()).ToList();
                    newPartitions.AddRange(subGroups);
                    if (subGroups.Count > 1) changed = true;
                }
                partitions = newPartitions;
            } while (changed);

            // Build minimized DFA
            var stateMap = new Dictionary<int, int>();
            var minimizedStates = new List<State>();
            int newId = 0;
            foreach (var group in partitions)
            {
                bool isStart = group.Any(id => states.First(s => s.Id == id).IsStart);
                bool isAccepting = group.Any(id => states.First(s => s.Id == id).IsAccepting);
                foreach (var oldId in group) stateMap[oldId] = newId;
                minimizedStates.Add(new State { Id = newId, IsStart = isStart, IsAccepting = isAccepting });
                newId++;
            }
            var minimizedTransitions = transitions
                .Where(t => stateMap.ContainsKey(t.FromStateId) && stateMap.ContainsKey(t.ToStateId))
                .Select(t => new Transition
                {
                    FromStateId = stateMap[t.FromStateId],
                    ToStateId = stateMap[t.ToStateId],
                    Symbol = t.Symbol
                })
                .Distinct()
                .ToList();
            var minimizedDfa = new DFA();
            minimizedDfa.States.AddRange(minimizedStates);
            minimizedDfa.Transitions.AddRange(minimizedTransitions);
            return minimizedDfa;
        }
    }
}
