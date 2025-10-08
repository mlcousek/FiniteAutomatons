using System;
using System.Collections.Generic;
using System.Linq;

namespace FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;

public class DFA : Automaton
{
    public override void StepForward(AutomatonExecutionState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.IsFinished || state.CurrentStateId == null)
        {
            state.IsAccepted = IsAcceptingState(state.CurrentStateId);
            return;
        }

        PushCurrentStateToHistory(state);

        char symbol = state.Input[state.Position];
        var transition = GetTransition(state.CurrentStateId.Value, symbol);

        if (transition == null)
        {
            // no transition for current symbol -> reject and finish execution
            state.IsAccepted = false;
            state.Position = state.Input.Length;
            return;
        }

        state.CurrentStateId = transition.ToStateId;
        state.Position++;

        if (state.Position >= state.Input.Length)
        {
            state.IsAccepted = IsAcceptingState(state.CurrentStateId);
        }
    }

    public override void StepBackward(AutomatonExecutionState state)
    {
        ArgumentNullException.ThrowIfNull(state);

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
            RecomputeCurrentStateUpToPosition(state);
        }

        state.IsAccepted = null;
    }

    public override void ExecuteAll(AutomatonExecutionState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (string.IsNullOrEmpty(state.Input))
        {
            state.IsAccepted = IsAcceptingState(state.CurrentStateId);
            return;
        }

        while (!state.IsFinished && state.IsAccepted != false)
        {
            StepForward(state);
        }
    }

    public override void BackToStart(AutomatonExecutionState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        state.Position = 0;
        state.CurrentStateId = States.FirstOrDefault(s => s.IsStart)?.Id;
        state.IsAccepted = null;
        state.StateHistory.Clear();
    }

    public DFA MinimalizeDFA()
    {
        return PerformMinimization();
    }

    // --- Private helpers ---

    private Transition? GetTransition(int fromStateId, char symbol)
    {
        return Transitions.FirstOrDefault(t => t.FromStateId == fromStateId && t.Symbol == symbol);
    }

    private bool IsAcceptingState(int? stateId)
    {
        return stateId != null && States.Any(s => s.Id == stateId && s.IsAccepting);
    }

    private static void PushCurrentStateToHistory(AutomatonExecutionState state)
    {
        if (state.CurrentStateId == null) return;
        state.StateHistory.Push([state.CurrentStateId.Value]);
    }

    private void RecomputeCurrentStateUpToPosition(AutomatonExecutionState state)
    {
        var startStateId = States.FirstOrDefault(s => s.IsStart)?.Id;
        if (startStateId == null)
        {
            state.CurrentStateId = null;
            state.IsAccepted = false;
            return;
        }

        state.CurrentStateId = startStateId;

        for (int i = 0; i < state.Position; i++)
        {
            char symbol = state.Input[i];
            var transition = GetTransition(state.CurrentStateId.Value, symbol);
            if (transition == null)
            {
                state.CurrentStateId = null;
                state.IsAccepted = false;
                return;
            }

            state.CurrentStateId = transition.ToStateId;
        }
    }

    private DFA PerformMinimization()
    {
        var startState = States.FirstOrDefault(s => s.IsStart)?.Id;
        if (startState == null)
            throw new InvalidOperationException("No start state defined.");

        // 1) Compute reachable states from the start
        var reachable = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(startState.Value);
        reachable.Add(startState.Value);

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            foreach (var t in from t in Transitions.Where(t => t.FromStateId == cur)
                              where reachable.Add(t.ToStateId)
                              select t)
            {
                queue.Enqueue(t.ToStateId);
            }
        }

        var reachableStates = States.Where(s => reachable.Contains(s.Id)).ToList();
        var reachableTransitions = Transitions.Where(t => reachable.Contains(t.FromStateId) && reachable.Contains(t.ToStateId)).ToList();

        // 2) Initial partition: accepting vs non-accepting
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
                // Map signature -> states
                var splits = new Dictionary<string, HashSet<int>>();

                foreach (var stateId in group)
                {
                    var signatureParts = new List<string>(symbols.Count);
                    foreach (var sym in symbols)
                    {
                        var target = reachableTransitions.FirstOrDefault(t => t.FromStateId == stateId && t.Symbol == sym)?.ToStateId;
                        if (target == null)
                        {
                            signatureParts.Add("null");
                        }
                        else
                        {
                            var partIdx = partitions.FindIndex(p => p.Contains(target.Value));
                            signatureParts.Add(partIdx.ToString());
                        }
                    }

                    var signature = string.Join("|", signatureParts);
                    if (!splits.ContainsKey(signature))
                        splits[signature] = [];

                    splits[signature].Add(stateId);
                }

                if (splits.Count == 1)
                {
                    newPartitions.Add([.. group]);
                }
                else
                {
                    changed = true;
                    foreach (var part in splits.Values)
                        newPartitions.Add([.. part]);
                }
            }

            partitions = newPartitions;
        } while (changed);

        // 3) Build minimized DFA
        var stateMap = new Dictionary<int, int>();
        var minimizedDfa = new DFA();
        int newId = 1;

        foreach (var group in partitions)
        {
            foreach (var oldId in group)
                stateMap[oldId] = newId;

            var representativeId = group.First();
            var repState = reachableStates.First(s => s.Id == representativeId);

            minimizedDfa.AddState(new State
            {
                Id = newId,
                IsStart = group.Contains(startState.Value),
                IsAccepting = repState.IsAccepting
            });

            newId++;
        }

        // Ensure start state is set
        var startGroup = partitions.First(p => p.Contains(startState.Value));
        minimizedDfa.SetStartState(stateMap[startGroup.First()]);

        // add transitions
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
}
