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

        var symbol = state.Input[state.Position];
        var transition = GetTransition(state.CurrentStateId.Value, symbol);

        if (!TryAdvanceState(state, transition))
        {
            return; // state updated inside TryAdvanceState when failed
        }

        if (IsAtInputEnd(state))
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

    private static bool TryAdvanceState(AutomatonExecutionState state, Transition? transition)
    {
        if (transition == null)
        {
            // no transition for current symbol -> reject and finish execution
            state.IsAccepted = false;
            state.Position = state.Input.Length;
            return false;
        }

        state.CurrentStateId = transition.ToStateId;
        state.Position++;
        return true;
    }

    private static bool IsAtInputEnd(AutomatonExecutionState state) => state.Position >= state.Input.Length;

    // ---------------- Minimization (refactored into discrete steps) ----------------

    private DFA PerformMinimization()
    {
        var startState = States.FirstOrDefault(s => s.IsStart)?.Id
            ?? throw new InvalidOperationException("No start state defined.");

        var (reachableStates, reachableTransitions) = ComputeReachable(startState);
        var partitions = CreateInitialPartitions(reachableStates);
        var symbols = reachableTransitions.Select(t => t.Symbol).Distinct().ToList();
        partitions = RefinePartitions(partitions, reachableTransitions, symbols);
        return BuildMinimizedDfa(partitions, reachableStates, reachableTransitions, startState, symbols);
    }

    private (List<State> reachableStates, List<Transition> reachableTransitions) ComputeReachable(int startStateId)
    {
        var reachable = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(startStateId);
        reachable.Add(startStateId);

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            foreach (var t in Transitions.Where(t => t.FromStateId == cur))
            {
                if (reachable.Add(t.ToStateId))
                {
                    queue.Enqueue(t.ToStateId);
                }
            }
        }

        var reachableStates = States.Where(s => reachable.Contains(s.Id)).ToList();
        var reachableTransitions = Transitions.Where(t => reachable.Contains(t.FromStateId) && reachable.Contains(t.ToStateId)).ToList();
        return (reachableStates, reachableTransitions);
    }

    private static List<HashSet<int>> CreateInitialPartitions(List<State> reachableStates)
    {
        var accepting = reachableStates.Where(s => s.IsAccepting).Select(s => s.Id).ToHashSet();
        var nonAccepting = reachableStates.Where(s => !s.IsAccepting).Select(s => s.Id).ToHashSet();

        var partitions = new List<HashSet<int>>();
        if (accepting.Count > 0) partitions.Add([.. accepting]);
        if (nonAccepting.Count > 0) partitions.Add([.. nonAccepting]);
        return partitions;
    }

    private static List<HashSet<int>> RefinePartitions(List<HashSet<int>> partitions, List<Transition> transitions, List<char> symbols)
    {
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
                    var signatureParts = new List<string>(symbols.Count);
                    foreach (var sym in symbols)
                    {
                        var target = transitions.FirstOrDefault(t => t.FromStateId == stateId && t.Symbol == sym)?.ToStateId;
                        signatureParts.Add(target == null ? "null" : partitions.FindIndex(p => p.Contains(target.Value)).ToString());
                    }

                    var signature = string.Join("|", signatureParts);
                    if (!splits.TryGetValue(signature, out var bucket))
                    {
                        bucket = [];
                        splits[signature] = bucket;
                    }
                    bucket.Add(stateId);
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

        return partitions;
    }

    private static DFA BuildMinimizedDfa(List<HashSet<int>> partitions, List<State> reachableStates, List<Transition> reachableTransitions, int startStateId, List<char> symbols)
    {
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
                IsStart = group.Contains(startStateId),
                IsAccepting = repState.IsAccepting
            });

            newId++;
        }

        // Ensure start state is set
        minimizedDfa.SetStartState(stateMap[startStateId]);

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
