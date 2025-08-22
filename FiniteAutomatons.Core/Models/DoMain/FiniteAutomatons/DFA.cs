namespace FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;

public class DFA : Automaton
{
    public override void StepForward(AutomatonExecutionState state)
    {
        if (state.IsFinished || state.CurrentStateId == null)
        {
            state.IsAccepted = States.Any(s => s.Id == state.CurrentStateId && s.IsAccepting);
            return;
        }

        state.StateHistory.Push([state.CurrentStateId.Value]);

        char symbol = state.Input[state.Position];
        var transition = Transitions
            .FirstOrDefault(t => t.FromStateId == state.CurrentStateId && t.Symbol == symbol);

        if (transition == null)
        {
            state.IsAccepted = false;
            state.Position = state.Input.Length;
            return;
        }

        state.CurrentStateId = transition.ToStateId;
        state.Position++;

        if (state.Position >= state.Input.Length)
        {
            state.IsAccepted = States.Any(s => s.Id == state.CurrentStateId && s.IsAccepting);
        }
    }

    public override void StepBackward(AutomatonExecutionState state)
    {
        if (state.Position == 0)
            return;

        state.Position--;

        if (state.StateHistory.Count > 0)
        {
            var previousStates = state.StateHistory.Pop();
            state.CurrentStateId = previousStates.FirstOrDefault();
        }
        else
        {
            state.CurrentStateId = States.First(s => s.IsStart).Id;
            for (int i = 0; i < state.Position; i++)
            {
                char symbol = state.Input[i];
                var transition = Transitions
                    .FirstOrDefault(t => t.FromStateId == state.CurrentStateId && t.Symbol == symbol);

                if (transition == null)
                {
                    state.CurrentStateId = null;
                    state.IsAccepted = false;
                    return;
                }

                state.CurrentStateId = transition.ToStateId;
            }
        }

        state.IsAccepted = null;
    }

    public override void ExecuteAll(AutomatonExecutionState state)
    {
        if (string.IsNullOrEmpty(state.Input))
        {
            state.IsAccepted = States.Any(s => s.Id == state.CurrentStateId && s.IsAccepting);
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
        state.CurrentStateId = States.FirstOrDefault(s => s.IsStart)?.Id;
        state.IsAccepted = null;
        state.StateHistory.Clear();
    }

    public DFA MinimalizeDFA()
    {
        var reachable = new HashSet<int>();
        var queue = new Queue<int>();
        var startState = States.FirstOrDefault(s => s.IsStart)?.Id;
        if (startState != null)
        {
            queue.Enqueue(startState.Value);
            reachable.Add(startState.Value);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var t in Transitions.Where(t => t.FromStateId == current))
                {
                    if (reachable.Add(t.ToStateId))
                        queue.Enqueue(t.ToStateId);
                }
            }

            var reachableStates = States.Where(s => reachable.Contains(s.Id)).ToList();
            var reachableTransitions = Transitions.Where(t => reachable.Contains(t.FromStateId) && reachable.Contains(t.ToStateId)).ToList();

            var accepting = reachableStates.Where(s => s.IsAccepting).Select(s => s.Id).ToHashSet();
            var nonAccepting = reachableStates.Where(s => !s.IsAccepting).Select(s => s.Id).ToHashSet();

            var partitions = new List<HashSet<int>>();
            if (accepting.Count > 0) partitions.Add([.. accepting]);
            if (nonAccepting.Count > 0) partitions.Add([.. nonAccepting]);

            var symbols = reachableTransitions.Select(t => t.Symbol).Distinct().ToList();

            bool changed;
            do
            {
                changed = false;
                var newPartitions = new List<HashSet<int>>();

                foreach (var group in partitions)
                {
                    var splits = new Dictionary<string, HashSet<int>>();

                    foreach (var stateId in group)
                    {
                        var signature = string.Join("|", symbols.Select(sym =>
                        {
                            var target = reachableTransitions
                                .FirstOrDefault(t => t.FromStateId == stateId && t.Symbol == sym)?.ToStateId;
                            if (target == null)
                                return "null";
                            var partIdx = partitions.FindIndex(p => p.Contains(target.Value));
                            return partIdx.ToString();
                        }));

                        if (!splits.ContainsKey(signature))
                            splits[signature] = [];
                        splits[signature].Add(stateId);
                    }

                    if (splits.Count == 1)
                    {
                        newPartitions.Add(group);
                    }
                    else
                    {
                        changed = true;
                        newPartitions.AddRange(splits.Values);
                    }
                }

                partitions = newPartitions;
            } while (changed);

            var stateMap = new Dictionary<int, int>(); 
            var minimizedDfa = new DFA();
            int newId = 1;

            foreach (var group in partitions)
            {
                var rep = reachableStates.First(s => group.Contains(s.Id));
                foreach (var oldId in group)
                    stateMap[oldId] = newId;

                minimizedDfa.AddState(new State
                {
                    Id = newId,
                    IsStart = group.Contains(startState.Value),
                    IsAccepting = group.Any(id => reachableStates.First(s => s.Id == id).IsAccepting)
                });
                newId++;
            }

            var startGroup = partitions.First(p => p.Contains(startState.Value));
            minimizedDfa.SetStartState(stateMap[startGroup.First()]);

            foreach (var group in partitions)
            {
                var rep = group.First();
                foreach (var sym in symbols)
                {
                    var trans = reachableTransitions.FirstOrDefault(t => t.FromStateId == rep && t.Symbol == sym);
                    if (trans != null)
                    {
                        int fromId = stateMap[rep];
                        int toId = stateMap[trans.ToStateId];
                        if (!minimizedDfa.Transitions.Any(t => t.FromStateId == fromId && t.ToStateId == toId && t.Symbol == sym))
                            minimizedDfa.AddTransition(fromId, toId, sym);
                    }
                }
            }

            return minimizedDfa;
        }

        throw new InvalidOperationException("No start state defined.");
    }
}
