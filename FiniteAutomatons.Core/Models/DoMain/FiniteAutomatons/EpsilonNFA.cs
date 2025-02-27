using FiniteAutomatons.Core.Interfaces;

namespace FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons
{
    public class EpsilonNFA : IAutomaton
    {
        public List<State> States { get; } = new();
        public List<Transition> Transitions { get; } = new();

        public int? StartStateId => States.FirstOrDefault(s => s.IsStart)?.Id;

        private HashSet<int> EpsilonClosure(HashSet<int> states)
        {
            var closure = new HashSet<int>(states);

            var stack = new Stack<int>(states);
            while (stack.Count > 0)
            {
                var state = stack.Pop();
                var epsilonTransitions = Transitions
                    .Where(t => t.FromStateId == state && t.Symbol == '\0');

                foreach (var transition in epsilonTransitions)
                {
                    if (closure.Add(transition.ToStateId))
                    {
                        stack.Push(transition.ToStateId);
                    }
                }
            }

            return closure;
        }
        public bool Execute(string input)
        {
            var currentStates = EpsilonClosure(new HashSet<int> { StartStateId ?? throw new InvalidOperationException("No start state defined.") });

            foreach (var symbol in input)
            {
                var nextStates = new HashSet<int>();

                foreach (var state in currentStates)
                {
                    var transitions = Transitions
                        .Where(t => t.FromStateId == state && t.Symbol == symbol);

                    foreach (var transition in transitions)
                    {
                        nextStates.Add(transition.ToStateId);
                    }
                }

                currentStates = EpsilonClosure(nextStates);
            }

            return currentStates.Any(state => States.Any(s => s.Id == state && s.IsAccepting));
        }
    }
}
