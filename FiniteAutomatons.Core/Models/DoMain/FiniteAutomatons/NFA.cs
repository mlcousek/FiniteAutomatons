namespace FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;

public class NFA : Automaton
{
    public override bool Execute(string input)
    {
        var currentStates = GetInitialStates();

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

            currentStates = ProcessNextStates(nextStates);
        }

        return currentStates.Any(state => States.Any(s => s.Id == state && s.IsAccepting));
    }

    protected virtual HashSet<int> GetInitialStates()
    {
        return [StartStateId ?? throw new InvalidOperationException("No start state defined.")];
    }

    protected virtual HashSet<int> ProcessNextStates(HashSet<int> nextStates)
    {
        return nextStates;
    }
}
