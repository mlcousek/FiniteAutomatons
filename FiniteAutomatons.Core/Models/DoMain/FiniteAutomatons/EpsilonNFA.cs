namespace FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;

public class EpsilonNFA : NFA
{
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

    protected override HashSet<int> GetInitialStates()
    {
        return EpsilonClosure([StartStateId ?? throw new InvalidOperationException("No start state defined.")]);
    }

    protected override HashSet<int> ProcessNextStates(HashSet<int> nextStates)
    {
        return EpsilonClosure(nextStates);
    }

    // Helper method to add epsilon transitions
    public void AddEpsilonTransition(int fromStateId, int toStateId)
    {
        AddTransition(fromStateId, toStateId, '\0');
    }
}
