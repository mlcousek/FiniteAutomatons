namespace FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;

public class NFA : Automaton
{
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

        state.StateHistory.Push(state.CurrentStates != null ? [.. state.CurrentStates] : []);

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
            state.CurrentStates = state.StateHistory.Pop();
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

    public DFA ToDFA()
    {
        var dfa = new DFA();

        // 1. Gather all input symbols (excluding epsilon, if present)
        var symbols = Transitions
            .Select(t => t.Symbol)
            .Where(s => s != '\0')
            .Distinct()
            .ToList();

        // 2. Subset construction: map each set of NFA states to a DFA state id
        var stateSetToId = new Dictionary<string, int>();
        var idToStateSet = new Dictionary<int, HashSet<int>>();
        int nextDfaStateId = 1;

        // Helper to get a unique key for a set of states
        static string SetKey(HashSet<int> set)
        {
            return string.Join(",", set.OrderBy(x => x));
        }

        // 3. Start with the initial NFA state set
        var initialSet = GetInitialStates();
        var queue = new Queue<HashSet<int>>();
        queue.Enqueue(initialSet);

        var initialKey = SetKey(initialSet);
        stateSetToId[initialKey] = nextDfaStateId;
        idToStateSet[nextDfaStateId] = [.. initialSet];

        // Add DFA start state
        dfa.AddState(new State
        {
            Id = nextDfaStateId,
            IsStart = true,
            IsAccepting = initialSet.Any(nfaId => States.First(s => s.Id == nfaId).IsAccepting)
        });
        dfa.SetStartState(nextDfaStateId);
        nextDfaStateId++;

        // 4. Process the queue
        while (queue.Count > 0)
        {
            var currentSet = queue.Dequeue();
            var currentKey = SetKey(currentSet);
            int currentDfaId = stateSetToId[currentKey];

            foreach (var symbol in symbols)
            {
                // Compute the set of NFA states reachable from any state in currentSet on 'symbol'
                var nextSet = new HashSet<int>();
                foreach (var nfaState in currentSet)
                {
                    var transitions = Transitions
                        .Where(t => t.FromStateId == nfaState && t.Symbol == symbol);
                    foreach (var t in transitions)
                    {
                        nextSet.Add(t.ToStateId);
                    }
                }

                if (nextSet.Count == 0)
                    continue;

                var nextKey = SetKey(nextSet);
                if (!stateSetToId.ContainsKey(nextKey))
                {
                    // New DFA state for this set
                    stateSetToId[nextKey] = nextDfaStateId;
                    idToStateSet[nextDfaStateId] = [.. nextSet];

                    dfa.AddState(new State
                    {
                        Id = nextDfaStateId,
                        IsStart = false,
                        IsAccepting = nextSet.Any(nfaId => States.First(s => s.Id == nfaId).IsAccepting)
                    });
                    queue.Enqueue(nextSet);
                    nextDfaStateId++;
                }

                // Add DFA transition
                dfa.AddTransition(currentDfaId, stateSetToId[nextKey], symbol);
            }
        }

        return dfa;
    }

    public override AutomatonExecutionState StartExecution(string input)
    {
        return new AutomatonExecutionState(input, null, GetInitialStates());
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
