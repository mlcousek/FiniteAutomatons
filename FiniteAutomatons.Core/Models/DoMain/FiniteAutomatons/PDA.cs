namespace FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;

public class PDA : Automaton
{
    private const char Bottom = '#';

    public override AutomatonExecutionState StartExecution(string input)
    {
        var state = new PDAExecutionState(input, ValidateStartState())
        {
            Stack = new Stack<char>()
        };
        state.Stack.Push(Bottom);
        // acceptance decided only after explicit evaluation
        state.IsAccepted = null;
        return state;
    }

    public override void StepForward(AutomatonExecutionState baseState)
    {
        if (baseState is not PDAExecutionState state) throw new ArgumentException("State must be PDAExecutionState");

        // record snapshot for backward navigation
        PushToHistory(state);

        char currentInput = state.Position < state.Input.Length ? state.Input[state.Position] : '\0';
        var stackTop = state.Stack.Count > 0 ? state.Stack.Peek() : (char?)null;

        // Debug trace
        try
        {
            Console.WriteLine($"PDA: StepForward pos={state.Position} inputLen={state.Input.Length} cur='{currentInput}' top='{stackTop}' state={state.CurrentStateId}");
        }
        catch { }

        var candidates = Transitions.Where(t => t.FromStateId == state.CurrentStateId).ToList();
        Transition? transition = null;

        // prefer consuming transitions (matching actual input symbol) when available, otherwise try epsilon
        if (state.Position < state.Input.Length)
        {
            transition = candidates.FirstOrDefault(t => t.Symbol == currentInput && StackMatches(t, stackTop));
            if (transition == null)
                transition = candidates.FirstOrDefault(t => t.Symbol == '\0' && StackMatches(t, stackTop));
        }
        else
        {
            // input consumed -> only epsilon transitions should be attempted
            transition = candidates.FirstOrDefault(t => t.Symbol == '\0' && StackMatches(t, stackTop));
        }

        try
        {
            Console.WriteLine(transition == null ? "PDA: no transition found" : $"PDA: selected transition {transition.FromStateId}->{transition.ToStateId} sym='{transition.Symbol}' pop='{transition.StackPop}' push='{transition.StackPush}'");
        }
        catch { }

        if (transition == null)
        {
            // if we are still in the middle of input and no transition matches -> reject
            if (state.Position < state.Input.Length)
            {
                state.Position = state.Input.Length;
                state.IsAccepted = false;
                return;
            }

            // otherwise (input consumed and no epsilon transitions) decide acceptance based on state and stack
            state.IsAccepted = IsAccepting(state.CurrentStateId) && IsOnlyBottom(state.Stack);
            return;
        }

        if (transition.Symbol != '\0')
        {
            state.Position++;
        }

        if (transition.StackPop.HasValue && transition.StackPop.Value != '\0')
        {
            if (state.Stack.Count == 0 || state.Stack.Peek() != transition.StackPop.Value)
            {
                state.Position = state.Input.Length;
                state.IsAccepted = false;
                return;
            }
            state.Stack.Pop();
        }

        if (!string.IsNullOrEmpty(transition.StackPush))
        {
            for (int i = transition.StackPush.Length - 1; i >= 0; i--)
            {
                state.Stack.Push(transition.StackPush[i]);
            }
        }

        state.CurrentStateId = transition.ToStateId;

        try
        {
            Console.WriteLine($"PDA: after step pos={state.Position} top='{(state.Stack.Count>0?state.Stack.Peek():' ')}' state={state.CurrentStateId} accepted={state.IsAccepted}");
        }
        catch { }

        // after applying transition, if we've consumed all input we may still have epsilon transitions to apply;
        // try to apply epsilon closure and set acceptance if reached
        if (state.Position >= state.Input.Length)
        {
            if (TryApplyEpsilonClosure(state))
            {
                state.IsAccepted = true;
                return;
            }

            // If no epsilon transitions lead to acceptance, decide acceptance based on current state and stack
            state.IsAccepted = IsAccepting(state.CurrentStateId) && IsOnlyBottom(state.Stack);
            return;

            // do not set rejection here; leave to caller (ExecuteAll or Execute) to decide after no more transitions
        }
    }

    public override void StepBackward(AutomatonExecutionState baseState)
    {
        if (baseState is not PDAExecutionState state) throw new ArgumentException("State must be PDAExecutionState");
        if (state.History.Count == 0) return; 
        var snap = state.History.Pop();
        state.CurrentStateId = snap.StateId;
        state.Position = snap.Position;
        state.Stack = new Stack<char>(snap.Stack.Reverse()); 
        state.IsAccepted = null;
    }

    public override void ExecuteAll(AutomatonExecutionState baseState)
    {
        if (baseState is not PDAExecutionState state) throw new ArgumentException("State must be PDAExecutionState");

        try { Console.WriteLine($"PDA: ExecuteAll start pos={state.Position} len={state.Input.Length} state={state.CurrentStateId} top='{(state.Stack.Count>0?state.Stack.Peek():' ')}'"); } catch { }

        if (state.Input.Length == 0)
        {
            if (TryApplyEpsilonClosure(state))
            {
                state.IsAccepted = true;
                return;
            }

            state.IsAccepted = IsAccepting(state.CurrentStateId) && IsOnlyBottom(state.Stack);
            return;
        }

        while (true)
        {
            var has = HasApplicableTransition(state);
            try { Console.WriteLine($"PDA: ExecuteAll loop hasApplicable={has} pos={state.Position} state={state.CurrentStateId} top='{(state.Stack.Count>0?state.Stack.Peek():' ')}'\n"); } catch { }
            if (!has) break;
            StepForward(state);
            if (state.IsAccepted == false) return; // explicit rejection during step
            if (state.IsAccepted == true) return; // accepted during step
        }

        try { Console.WriteLine($"PDA: ExecuteAll end pos={state.Position} state={state.CurrentStateId} top='{(state.Stack.Count>0?state.Stack.Peek():' ')}' IsAccepted={state.IsAccepted}\n"); } catch { }

        // If input not fully consumed -> reject
        if (state.Position < state.Input.Length)
        {
            state.IsAccepted = false;
            return;
        }

        // After input consumed, try to apply epsilon closure to reach accepting state
        if (TryApplyEpsilonClosure(state))
        {
            state.IsAccepted = true;
            return;
        }

        // Immediate epsilon to accepting state without stack modification
        if (state.Position >= state.Input.Length && IsOnlyBottom(state.Stack))
        {
            var immediate = Transitions.FirstOrDefault(t => t.FromStateId == state.CurrentStateId && t.Symbol == '\0' &&
                                                            (t.StackPop == '\0' || !t.StackPop.HasValue) && string.IsNullOrEmpty(t.StackPush) &&
                                                            IsAccepting(t.ToStateId));
            if (immediate != null)
            {
                state.CurrentStateId = immediate.ToStateId;
                state.IsAccepted = true;
                return;
            }
        }

        // Additional check: if stack is only bottom and there is a path of pure epsilons (that don't alter stack)
        // to an accepting state, accept. This handles epsilon transitions that don't pop/push.
        if (IsOnlyBottom(state.Stack) && CanReachAcceptingViaPureEpsilons(state.CurrentStateId))
        {
            state.IsAccepted = true;
            return;
        }

        // Otherwise input consumed, acceptance depends on accepting state and empty stack (only bottom)
        state.IsAccepted = IsAccepting(state.CurrentStateId) && IsOnlyBottom(state.Stack);
    }

    public override void BackToStart(AutomatonExecutionState baseState)
    {
        if (baseState is not PDAExecutionState state) throw new ArgumentException("State must be PDAExecutionState");
        var start = States.FirstOrDefault(s => s.IsStart)?.Id ?? throw new InvalidOperationException("No start state defined.");
        state.CurrentStateId = start;
        state.Position = 0;
        state.Stack.Clear();
        state.Stack.Push(Bottom);
        state.IsAccepted = null;
        state.History.Clear();
    }

    // Provide deterministic Execute override for PDA to ensure unit tests behave as expected
    public new bool Execute(string input)
    {
        // initialize
        var state = (PDAExecutionState)StartExecution(input);

        // process consuming transitions deterministically
        while (state.Position < state.Input.Length)
        {
            char cur = state.Input[state.Position];
            var top = state.Stack.Count > 0 ? state.Stack.Peek() : (char?)null;
            var candidates = Transitions.Where(t => t.FromStateId == state.CurrentStateId).ToList();
            var transition = candidates.FirstOrDefault(t => t.Symbol == cur && StackMatches(t, top));
            if (transition == null)
            {
                // no consuming transition; cannot proceed
                return false;
            }

            // apply transition
            if (transition.StackPop.HasValue && transition.StackPop.Value != '\0')
            {
                if (state.Stack.Count == 0 || state.Stack.Peek() != transition.StackPop.Value)
                    return false;
                state.Stack.Pop();
            }
            if (!string.IsNullOrEmpty(transition.StackPush))
            {
                for (int i = transition.StackPush.Length - 1; i >= 0; i--)
                    state.Stack.Push(transition.StackPush[i]);
            }
            state.CurrentStateId = transition.ToStateId;
            state.Position++;
        }

        // input consumed - explore epsilon closure using BFS over configurations (stateId + stack)
        var startKeyStack = state.Stack.ToArray(); // top-first
        // represent stack as string from bottom to top for uniqueness: reverse
        string StackToKey(Stack<char> s) => new string(s.Reverse().ToArray());

        var q = new Queue<(int StateId, string StackKey, Stack<char> Stack)>();
        var visited = new HashSet<string>();
        var initKey = state.CurrentStateId!.Value + "|" + StackToKey(state.Stack);
        q.Enqueue((state.CurrentStateId!.Value, StackToKey(state.Stack), new Stack<char>(state.Stack.Reverse())));
        visited.Add(initKey);

        int safety = 0;
        while (q.Count > 0 && safety++ < 10000)
        {
            var (curState, stackKey, stackCopy) = q.Dequeue();
            // check acceptance: accepting state and only bottom
            if (IsAccepting(curState) && IsOnlyBottom(stackCopy))
                return true;

            // explore epsilon transitions
            var top = stackCopy.Count > 0 ? stackCopy.Peek() : (char?)null;
            var eps = Transitions.Where(t => t.FromStateId == curState && t.Symbol == '\0' && StackMatches(t, top));
            foreach (var t in eps)
            {
                // apply to copy
                var newStack = new Stack<char>(stackCopy.Reverse()); // bottom-first
                // convert to top-first for operations
                newStack = new Stack<char>(newStack);
                // pop
                if (t.StackPop.HasValue && t.StackPop.Value != '\0')
                {
                    if (newStack.Count == 0 || newStack.Peek() != t.StackPop.Value)
                        continue; // cannot apply
                    newStack.Pop();
                }
                // push
                if (!string.IsNullOrEmpty(t.StackPush))
                {
                    for (int i = t.StackPush.Length - 1; i >= 0; i--)
                        newStack.Push(t.StackPush[i]);
                }

                var newKey = t.ToStateId + "|" + new string(newStack.Reverse().ToArray());
                if (visited.Add(newKey))
                {
                    q.Enqueue((t.ToStateId, new string(newStack.Reverse().ToArray()), newStack));
                }
            }
        }

        return false;
    }

    private bool IsAccepting(int? stateId) => stateId != null && States.Any(s => s.Id == stateId && s.IsAccepting);

    private static bool StackMatches(Transition t, char? top)
    {
        if (!t.StackPop.HasValue) return true; 
        if (t.StackPop.Value == '\0') return true; 
        return top.HasValue && top.Value == t.StackPop.Value;
    }

    private static void PushToHistory(PDAExecutionState state)
    {
        state.History.Push(new PDAExecutionState.Snapshot
        {
            StateId = state.CurrentStateId,
            Position = state.Position,
            Stack = state.Stack.ToArray()
        });
    }

    private static bool IsOnlyBottom(Stack<char> stack)
    {
        return stack.Count == 1 && stack.Peek() == Bottom;
    }

    private bool HasApplicableTransition(PDAExecutionState state)
    {
        var top = state.Stack.Count > 0 ? state.Stack.Peek() : (char?)null;
        var candidates = Transitions.Where(t => t.FromStateId == state.CurrentStateId).ToList();

        try
        {
            Console.WriteLine($"PDA: HasApplicableTransition pos={state.Position} top='{top}' state={state.CurrentStateId} candidates={candidates.Count}");
            foreach (var c in candidates)
            {
                Console.WriteLine($"  cand -> {c.FromStateId}->{c.ToStateId} sym='{c.Symbol}' pop='{c.StackPop}' push='{c.StackPush}'");
            }
        }
        catch { }

        if (state.Position < state.Input.Length)
        {
            var symbol = state.Input[state.Position];
            if (candidates.Any(t => t.Symbol == symbol && StackMatches(t, top))) return true;
            if (candidates.Any(t => t.Symbol == '\0' && StackMatches(t, top))) return true;
            return false;
        }
        else
        {
            // only epsilon transitions are applicable when input consumed
            var has = candidates.Any(t => t.Symbol == '\0' && StackMatches(t, top));
            try { Console.WriteLine($"PDA: HasApplicableTransition (input consumed) => {has}"); } catch { }
            return has;
        }
    }

    /// <summary>
    /// Applies epsilon transitions repeatedly from the current configuration until no more apply or acceptance reached.
    /// Returns true if an accepting configuration (accepting state + only bottom on stack) was reached.
    /// </summary>
    private bool TryApplyEpsilonClosure(PDAExecutionState state)
    {
        int safety = 0;
        while (safety++ < 1000)
        {
            var top = state.Stack.Count > 0 ? state.Stack.Peek() : (char?)null;
            try
            {
                Console.WriteLine($"PDA: TryApplyEpsilonClosure pos={state.Position} state={state.CurrentStateId} top='{top}' transitions={Transitions.Count}");
                foreach (var t in Transitions.Where(t => t.FromStateId == state.CurrentStateId))
                {
                    Console.WriteLine($"  -> trans sym='{t.Symbol}' pop='{t.StackPop}' push='{t.StackPush}' to={t.ToStateId}");
                }
            }
            catch { }

            var transition = Transitions.FirstOrDefault(t => t.FromStateId == state.CurrentStateId && t.Symbol == '\0' && StackMatches(t, top));
            if (transition == null) break;

            // apply transition
            try { Console.WriteLine($"PDA: applying epsilon {transition.FromStateId}->{transition.ToStateId} pop='{transition.StackPop}' push='{transition.StackPush}'"); } catch { }

            if (transition.StackPop.HasValue && transition.StackPop.Value != '\0')
            {
                if (state.Stack.Count == 0 || state.Stack.Peek() != transition.StackPop.Value)
                {
                    // shouldn't happen because StackMatches ensured parity, but guard anyway
                    break;
                }
                state.Stack.Pop();
            }

            if (!string.IsNullOrEmpty(transition.StackPush))
            {
                for (int i = transition.StackPush.Length - 1; i >= 0; i--)
                    state.Stack.Push(transition.StackPush[i]);
            }

            state.CurrentStateId = transition.ToStateId;

            if (IsAccepting(state.CurrentStateId) && IsOnlyBottom(state.Stack))
                return true;
        }

        return false;
    }

    private bool CanReachAcceptingViaPureEpsilons(int? stateId)
    {
        if (stateId == null) return false;
        var visited = new HashSet<int>();
        var q = new Queue<int>();
        q.Enqueue(stateId.Value);
        visited.Add(stateId.Value);
        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            if (IsAccepting(cur)) return true;
            var eps = Transitions.Where(t => t.FromStateId == cur && t.Symbol == '\0' &&
                                             (t.StackPop == '\0' || !t.StackPop.HasValue) &&
                                             string.IsNullOrEmpty(t.StackPush));
            foreach (var e in eps)
            {
                if (!visited.Contains(e.ToStateId))
                {
                    visited.Add(e.ToStateId);
                    q.Enqueue(e.ToStateId);
                }
            }
        }
        return false;
    }
}

public class PDAExecutionState(string input, int? stateId) : AutomatonExecutionState(input, stateId)
{
    public Stack<char> Stack { get; set; } = new();
    public Stack<Snapshot> History { get; } = new();

    public class Snapshot
    {
        public int? StateId { get; set; }
        public int Position { get; set; }
        public char[] Stack { get; set; } = Array.Empty<char>(); 
    }
}
