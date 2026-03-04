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
        var symbols = GetNonEpsilonSymbols();
        var conversionContext = InitializeConversionContext();

        CreateInitialDfaState(dfa, conversionContext);
        ProcessStateSetQueue(dfa, conversionContext, symbols);

        return dfa;
    }

    private List<char> GetNonEpsilonSymbols()
    {
        return [.. Transitions
            .Select(t => t.Symbol)
            .Where(s => s != '\0')
            .Distinct()];
    }

    private static SubsetConstructionContext InitializeConversionContext()
    {
        return new SubsetConstructionContext
        {
            StateSetToId = [],
            IdToStateSet = [],
            NextDfaStateId = 1,
            Queue = new Queue<HashSet<int>>()
        };
    }

    private void CreateInitialDfaState(DFA dfa, SubsetConstructionContext context)
    {
        var initialSet = GetInitialStates();
        context.Queue.Enqueue(initialSet);

        var initialKey = CreateSetKey(initialSet);
        context.StateSetToId[initialKey] = context.NextDfaStateId;
        context.IdToStateSet[context.NextDfaStateId] = [.. initialSet];

        dfa.AddState(new State
        {
            Id = context.NextDfaStateId,
            IsStart = true,
            IsAccepting = IsAcceptingStateSet(initialSet)
        });
        dfa.SetStartState(context.NextDfaStateId);
        context.NextDfaStateId++;
    }

    private void ProcessStateSetQueue(DFA dfa, SubsetConstructionContext context, List<char> symbols)
    {
        while (context.Queue.Count > 0)
        {
            var currentSet = context.Queue.Dequeue();
            var currentKey = CreateSetKey(currentSet);
            int currentDfaId = context.StateSetToId[currentKey];

            ProcessTransitionsForStateSet(dfa, context, currentSet, currentDfaId, symbols);
        }
    }

    private void ProcessTransitionsForStateSet(DFA dfa, SubsetConstructionContext context,
        HashSet<int> currentSet, int currentDfaId, List<char> symbols)
    {
        foreach (var symbol in symbols)
        {
            var nextSet = ComputeNextStateSet(currentSet, symbol);

            if (nextSet.Count == 0)
                continue;

            int targetDfaId = GetOrCreateDfaState(dfa, context, nextSet);
            dfa.AddTransition(currentDfaId, targetDfaId, symbol);
        }
    }

    private HashSet<int> ComputeNextStateSet(HashSet<int> currentSet, char symbol)
    {
        var nextSet = new HashSet<int>();

        foreach (var nfaState in currentSet)
        {
            var transitions = Transitions.Where(t => t.FromStateId == nfaState && t.Symbol == symbol);
            foreach (var transition in transitions)
            {
                nextSet.Add(transition.ToStateId);
            }
        }

        return nextSet;
    }

    private int GetOrCreateDfaState(DFA dfa, SubsetConstructionContext context, HashSet<int> stateSet)
    {
        var key = CreateSetKey(stateSet);

        if (context.StateSetToId.TryGetValue(key, out int existingId))
        {
            return existingId;
        }

        int newId = context.NextDfaStateId;
        context.StateSetToId[key] = newId;
        context.IdToStateSet[newId] = [.. stateSet];

        dfa.AddState(new State
        {
            Id = newId,
            IsStart = false,
            IsAccepting = IsAcceptingStateSet(stateSet)
        });

        context.Queue.Enqueue(stateSet);
        context.NextDfaStateId++;

        return newId;
    }

    private bool IsAcceptingStateSet(HashSet<int> stateSet)
    {
        return stateSet.Any(nfaId => States.First(s => s.Id == nfaId).IsAccepting);
    }

    private static string CreateSetKey(HashSet<int> set)
    {
        return string.Join(",", set.OrderBy(x => x));
    }

    private class SubsetConstructionContext
    {
        public Dictionary<string, int> StateSetToId { get; init; } = null!;
        public Dictionary<int, HashSet<int>> IdToStateSet { get; init; } = null!;
        public int NextDfaStateId { get; set; }
        public Queue<HashSet<int>> Queue { get; init; } = null!;
    }

    public override AutomatonExecutionState StartExecution(string input, Stack<char>? initialStack = null)
    {
        var state = new AutomatonExecutionState(input, null, GetInitialStates());

        if (string.IsNullOrEmpty(input))
        {
            state.IsAccepted = state.CurrentStates != null && state.CurrentStates.Any(sid => States.Any(st => st.Id == sid && st.IsAccepting));
        }

        return state;
    }

    protected virtual HashSet<int> GetInitialStates()
    {
        return [StartStateId ?? throw new InvalidOperationException("No start state defined.")];
    }

    protected virtual HashSet<int> ProcessNextStates(HashSet<int> nextStates)
    {
        return nextStates;
    }

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
