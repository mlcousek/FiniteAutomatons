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

    public override void StepForward(AutomatonExecutionState state)
    {
        if (state.IsFinished || state.Position >= state.Input.Length)
        {
            if (state.CurrentStates != null)
            {
                state.IsAccepted = state.CurrentStates.Any(s => States.Any(st => st.Id == s && st.IsAccepting));
            }
            return;
        }


        // Always push the closure of the current states to history
        state.StateHistory.Push(state.CurrentStates != null ? [.. ProcessNextStates(state.CurrentStates)] : []);

        char symbol = state.Input[state.Position];
        var nextStates = new HashSet<int>();

        foreach (var currentState in state.CurrentStates ?? [])
        {
            var transitions = Transitions
                .Where(t => t.FromStateId == currentState && t.Symbol == symbol);

            foreach (var transition in transitions)
            {
                nextStates.Add(transition.ToStateId);
            }
        }

        state.CurrentStates = ProcessNextStates(nextStates);
        state.Position++;

        if (state.Position >= state.Input.Length)
        {
            state.IsAccepted = state.CurrentStates != null &&
                state.CurrentStates.Any(s => States.Any(st => st.Id == s && st.IsAccepting));
        }
    }

    public override void StepBackward(AutomatonExecutionState state)
    {
        if (state.Position == 0)
            return;

        state.Position--;

        // Restore the previous set of states from the history stack
        if (state.StateHistory.Count > 0)
        {
            state.CurrentStates = ProcessNextStates(state.StateHistory.Pop());

            state.CurrentStates = ProcessNextStates(state.CurrentStates);


        }
        else
        {
            // Fallback: recalculate from start if history is missing
            state.CurrentStates = GetInitialStates();
            for (int i = 0; i < state.Position; i++)
            {
                char symbol = state.Input[i];
                var nextStates = new HashSet<int>();
                foreach (var currentState in state.CurrentStates ?? [])
                {
                    var transitions = Transitions
                        .Where(t => t.FromStateId == currentState && t.Symbol == symbol);
                    foreach (var transition in transitions)
                    {
                        nextStates.Add(transition.ToStateId);
                    }
                }
                state.CurrentStates = ProcessNextStates(nextStates);
            }
        }

        state.IsAccepted = null;
    }

    public override void ExecuteAll(AutomatonExecutionState state)
    {
        if (state.Input.Length == 0)
        {
            state.IsAccepted = state.CurrentStates != null &&
                state.CurrentStates.Any(s => States.Any(st => st.Id == s && st.IsAccepting));
            return;
        }

        while (!state.IsFinished && state.IsAccepted != false)
        {
            StepForward(state);
        }
    }

    // Helper method to add epsilon transitions
    public void AddEpsilonTransition(int fromStateId, int toStateId)
    {
        AddTransition(fromStateId, toStateId, '\0');
    }

    public override void BackToStart(AutomatonExecutionState state)
    {
        state.Position = 0;
        state.CurrentStates = GetInitialStates();
        state.CurrentStateId = null;
        state.IsAccepted = null;
        state.StateHistory.Clear();
    }
}
