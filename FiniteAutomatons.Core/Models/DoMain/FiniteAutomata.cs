namespace FiniteAutomatons.Core.Models.DoMain
{
    public class FiniteAutomata
    {
        public List<State> States { get; } = new();
        public List<Transition> Transitions { get; } = new();

        public int? StartStateId => States.FirstOrDefault(s => s.IsStart)?.Id;

        /// <summary>
        /// Executes the automaton on a given input.
        /// </summary>
        public bool Execute(string input)
        {
            // For a DFA you only have one current state at a time.
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

            // Check if the final state is an accepting state.
            return States.Any(s => s.Id == currentStateId && s.IsAccepting);
        }
    }
}
