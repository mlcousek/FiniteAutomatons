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
            foreach (var transition in from transition in epsilonTransitions
                                       where closure.Add(transition.ToStateId)
                                       select transition)
            {
                stack.Push(transition.ToStateId);
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
            FinalizeAcceptanceIfFinished(state);
            return;
        }

        PushCurrentStatesToHistory(state);

        char symbol = state.Input[state.Position];
        var nextStates = ComputeNextStates(state.CurrentStates, symbol);

        state.CurrentStates = ProcessNextStates(nextStates);
        state.Position++;

        if (state.Position >= state.Input.Length)
        {
            EvaluateAcceptance(state);
        }
    }

    public override void StepBackward(AutomatonExecutionState state)
    {
        if (state.Position == 0)
            return;

        state.Position--;

        if (state.StateHistory.Count > 0)
        {
            state.CurrentStates = state.StateHistory.Pop();
        }
        else
        {
            RecomputeStatesUpToPosition(state);
        }

        state.IsAccepted = null;
    }

    public override void ExecuteAll(AutomatonExecutionState state)
    {
        if (state.Input.Length == 0)
        {
            EvaluateAcceptance(state);
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

        foreach (var stateId in States.Select(state => state.Id))
        {
            var closure = EpsilonClosure([stateId]);
            bool isAccepting = closure.Any(id => States.First(s => s.Id == id).IsAccepting);
            var state = States.First(s => s.Id == stateId);
            nfa.AddState(new State
            {
                Id = state.Id,
                IsStart = state.IsStart,
                IsAccepting = isAccepting
            });
            if (state.IsStart)
                nfa.SetStartState(state.Id);
        }

        var symbols = Transitions
            .Where(t => t.Symbol != '\0')
            .Select(t => t.Symbol)
            .Distinct()
            .ToList();

        foreach (var stateId in States.Select(state => state.Id))
        {
            var fromClosure = EpsilonClosure([stateId]);
            foreach (var symbol in symbols)
            {
                var toStates = new HashSet<int>();
                foreach (var closureState in fromClosure)
                {
                    var transitions = Transitions
                        .Where(t => t.FromStateId == closureState && t.Symbol == symbol);
                    foreach (var t in transitions)
                    {
                        foreach (var target in EpsilonClosure([t.ToStateId]))
                            toStates.Add(target);
                    }
                }
                foreach (var to in toStates)
                {
                    nfa.AddTransition(stateId, to, symbol);
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

    private void FinalizeAcceptanceIfFinished(AutomatonExecutionState state)
    {
        if (state.CurrentStates != null)
        {
            state.IsAccepted = state.CurrentStates.Any(s => States.Any(st => st.Id == s && st.IsAccepting));
        }
    }

    private static void PushCurrentStatesToHistory(AutomatonExecutionState state)
    {
        state.StateHistory.Push(state.CurrentStates != null ? [.. state.CurrentStates] : []);
    }

    private HashSet<int> ComputeNextStates(HashSet<int>? currentStates, char symbol)
    {
        var nextStates = new HashSet<int>();

        foreach (var currentState in currentStates ?? [])
        {
            var transitions = Transitions
                .Where(t => t.FromStateId == currentState && t.Symbol == symbol);

            foreach (var transition in transitions)
            {
                nextStates.Add(transition.ToStateId);
            }
        }

        return nextStates;
    }

    private void EvaluateAcceptance(AutomatonExecutionState state)
    {
        state.IsAccepted = state.CurrentStates != null &&
            state.CurrentStates.Any(s => States.Any(st => st.Id == s && st.IsAccepting));
    }

    private void RecomputeStatesUpToPosition(AutomatonExecutionState state)
    {
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
}
