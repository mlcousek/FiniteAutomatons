using System.Collections.Generic;
using System.Linq;

namespace FiniteAutomatons.Core.Models.DoMain
{
    public class DFA : Automaton
    {
        public override bool Execute(string input)
        {
            int? currentStateId = StartStateId;
            if (currentStateId == null) return false;

            foreach (char symbol in input)
            {
                var transition = Transitions.FirstOrDefault(t => t.FromStateId == currentStateId && t.Symbol == symbol);
                if (transition == null) return false;
                currentStateId = transition.ToStateId;
            }

            var currentState = States.FirstOrDefault(s => s.Id == currentStateId);
            return currentState != null && currentState.IsAccepting;
        }

        public List<int> GetStepwiseExecution(string input)
        {
            var stateSequence = new List<int>();
            int? currentStateId = StartStateId;
            if (currentStateId == null) return stateSequence;
            stateSequence.Add(currentStateId.Value);
            foreach (char symbol in input)
            {
                var transition = Transitions.FirstOrDefault(t => t.FromStateId == currentStateId && t.Symbol == symbol);
                if (transition == null) break;
                currentStateId = transition.ToStateId;
                stateSequence.Add(currentStateId.Value);
            }
            return stateSequence;
        }
    }
}
