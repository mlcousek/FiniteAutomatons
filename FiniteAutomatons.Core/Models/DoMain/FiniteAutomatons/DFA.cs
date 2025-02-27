using FiniteAutomatons.Core.Interfaces;

namespace FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons
{
    public class DFA : IAutomaton
    {
        public List<State> States { get; } = new();
        public List<Transition> Transitions { get; } = new();

        public int? StartStateId => States.FirstOrDefault(s => s.IsStart)?.Id;

        public bool Execute(string input)
        {
            var currentStateId = StartStateId;
            if (currentStateId == null)
            {
                throw new InvalidOperationException("No start state defined.");
            }

            foreach (var symbol in input)
            {
                var transition = Transitions
                    .FirstOrDefault(t => t.FromStateId == currentStateId && t.Symbol == symbol);
                if (transition == null)
                {
                    return false;
                }

                currentStateId = transition.ToStateId;
            }

            return States.Any(s => s.Id == currentStateId && s.IsAccepting);
        }
    }
}
