namespace FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;

public class NFA : Automaton
{
    public override bool Execute(string input)
    {
        var currentStates = new HashSet<int> { StartStateId ?? throw new InvalidOperationException("No start state defined.") };

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

            currentStates = nextStates;
        }

        return currentStates.Any(state => States.Any(s => s.Id == state && s.IsAccepting));
    }
}
