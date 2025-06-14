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

    public override void BackToStart(AutomatonExecutionState state)
    {
        state.Position = 0;
        state.CurrentStates = GetInitialStates();
        state.CurrentStateId = null;
        state.IsAccepted = null;
        state.StateHistory.Clear();
    }

    public NFA ToNFA()
    {
        var nfa = new NFA();

        // 1. Copy all states, preserving IDs and accepting/start flags
        foreach (var state in States)
        {
            // Accepting if any state in its epsilon-closure is accepting
            var closure = EpsilonClosure([state.Id]);
            bool isAccepting = closure.Any(id => States.First(s => s.Id == id).IsAccepting);
            nfa.AddState(new State
            {
                Id = state.Id,
                IsStart = state.IsStart,
                IsAccepting = isAccepting
            });
            if (state.IsStart)
                nfa.SetStartState(state.Id);
        }

        // 2. For each state and each symbol (except epsilon), add transitions according to epsilon-closure
        var symbols = Transitions
            .Where(t => t.Symbol != '\0')
            .Select(t => t.Symbol)
            .Distinct()
            .ToList();

        foreach (var state in States)
        {
            var fromClosure = EpsilonClosure([state.Id]);
            foreach (var symbol in symbols)
            {
                // All states reachable from the closure of 'state' via 'symbol'
                var toStates = new HashSet<int>();
                foreach (var closureState in fromClosure)
                {
                    var transitions = Transitions
                        .Where(t => t.FromStateId == closureState && t.Symbol == symbol);
                    foreach (var t in transitions)
                    {
                        // Add all states in the epsilon-closure of the target
                        foreach (var target in EpsilonClosure([t.ToStateId]))
                            toStates.Add(target);
                    }
                }
                foreach (var to in toStates)
                {
                    nfa.AddTransition(state.Id, to, symbol);
                }
            }
        }

        return nfa;
    }

    // Helper method to add epsilon transitions
    public void AddEpsilonTransition(int fromStateId, int toStateId)
    {
        AddTransition(fromStateId, toStateId, '\0');
    }
}
