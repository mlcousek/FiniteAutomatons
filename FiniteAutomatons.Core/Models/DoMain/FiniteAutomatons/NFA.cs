namespace FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;

public class NFA : Automaton
{
    public override void StepForward(AutomatonExecutionState state)
    {
        if (state.IsFinished || state.Position >= state.Input.Length)
        {
            FinalizeAcceptanceIfFinished(state);
            return;
        }

        PushCurrentStatesToHistory(state);

        var symbol = state.Input[state.Position];
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

    public DFA ToDFA()
    {
        var dfa = new DFA();

        var symbols = Transitions
            .Select(t => t.Symbol)
            .Where(s => s != '\0')
            .Distinct()
            .ToList();

        var stateSetToId = new Dictionary<string, int>();
        var idToStateSet = new Dictionary<int, HashSet<int>>();
        int nextDfaStateId = 1;

        static string SetKey(HashSet<int> set) => string.Join(",", set.OrderBy(x => x));

        var initialSet = GetInitialStates();
        var queue = new Queue<HashSet<int>>();
        queue.Enqueue(initialSet);

        var initialKey = SetKey(initialSet);
        stateSetToId[initialKey] = nextDfaStateId;
        idToStateSet[nextDfaStateId] = [.. initialSet];

        dfa.AddState(new State
        {
            Id = nextDfaStateId,
            IsStart = true,
            IsAccepting = initialSet.Any(nfaId => States.First(s => s.Id == nfaId).IsAccepting)
        });
        dfa.SetStartState(nextDfaStateId);
        nextDfaStateId++;

        while (queue.Count > 0)
        {
            var currentSet = queue.Dequeue();
            var currentKey = SetKey(currentSet);
            int currentDfaId = stateSetToId[currentKey];

            foreach (var symbol in symbols)
            {
                var nextSet = new HashSet<int>();
                foreach (var nfaState in currentSet)
                {
                    foreach (var t in Transitions.Where(t => t.FromStateId == nfaState && t.Symbol == symbol))
                    {
                        nextSet.Add(t.ToStateId);
                    }
                }

                if (nextSet.Count == 0)
                    continue;

                var nextKey = SetKey(nextSet);
                if (!stateSetToId.TryGetValue(nextKey, out int value))
                {
                    value = nextDfaStateId;
                    stateSetToId[nextKey] = value;
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

                dfa.AddTransition(currentDfaId, value, symbol);
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

    // ---------------- Helper Methods ----------------

    private void FinalizeAcceptanceIfFinished(AutomatonExecutionState state)
    {
        if (state.CurrentStates != null)
        {
            EvaluateAcceptance(state);
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
            foreach (var t in Transitions.Where(t => t.FromStateId == currentState && t.Symbol == symbol))
            {
                nextStates.Add(t.ToStateId);
            }
        }
        return nextStates;
    }

    private void RecomputeStatesUpToPosition(AutomatonExecutionState state)
    {
        state.CurrentStates = GetInitialStates();
        for (int i = 0; i < state.Position; i++)
        {
            var symbol = state.Input[i];
            var nextStates = ComputeNextStates(state.CurrentStates, symbol);
            state.CurrentStates = ProcessNextStates(nextStates);
        }
    }

    private void EvaluateAcceptance(AutomatonExecutionState state)
    {
        state.IsAccepted = state.CurrentStates != null &&
                            state.CurrentStates.Any(s => States.Any(st => st.Id == s && st.IsAccepting));
    }
}
