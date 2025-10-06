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

        if (state.StateHistory.Count > 0)
        {
            state.CurrentStates = state.StateHistory.Pop();
        }
        else
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

        var symbols = Transitions
            .Select(t => t.Symbol)
            .Where(s => s != '\0')
            .Distinct()
            .ToList();

        var stateSetToId = new Dictionary<string, int>();
        var idToStateSet = new Dictionary<int, HashSet<int>>();
        int nextDfaStateId = 1;

        static string SetKey(HashSet<int> set)
        {
            return string.Join(",", set.OrderBy(x => x));
        }

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
}
