using System.Collections.Generic;
using System.Linq;

namespace FiniteAutomatons.Core.Models.DoMain
{
    public class NFA : Automaton
    {
        public override bool Execute(string input)
        {
            var currentStates = new HashSet<int?> { StartStateId };
            foreach (char symbol in input)
            {
                var nextStates = new HashSet<int?>();
                foreach (var stateId in currentStates)
                {
                    var transitions = Transitions.Where(t => t.FromStateId == stateId && t.Symbol == symbol);
                    foreach (var t in transitions)
                        nextStates.Add(t.ToStateId);
                }
                if (nextStates.Count == 0) return false;
                currentStates = nextStates;
            }
            return currentStates.Any(id => States.FirstOrDefault(s => s.Id == id)?.IsAccepting == true);
        }

        public List<HashSet<int?>> GetStepwiseExecution(string input)
        {
            var steps = new List<HashSet<int?>>();
            var currentStates = new HashSet<int?> { StartStateId };
            steps.Add(new HashSet<int?>(currentStates));
            foreach (char symbol in input)
            {
                var nextStates = new HashSet<int?>();
                foreach (var stateId in currentStates)
                {
                    var transitions = Transitions.Where(t => t.FromStateId == stateId && t.Symbol == symbol);
                    foreach (var t in transitions)
                        nextStates.Add(t.ToStateId);
                }
                currentStates = nextStates;
                steps.Add(new HashSet<int?>(currentStates));
            }
            return steps;
        }
    }
}
