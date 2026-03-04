namespace FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;

public class PDA : Automaton
{
    private const char Bottom = '#';

    public PDAAcceptanceMode AcceptanceMode { get; set; } = PDAAcceptanceMode.FinalStateAndEmptyStack;

    private Stack<char>? initialStack;

    public override AutomatonExecutionState StartExecution(string input, Stack<char>? initialStack = null)
    {
        var state = new PDAExecutionState(input, ValidateStartState())
        {
            Stack = new Stack<char>()
        };

        if (initialStack != null && initialStack.Count > 0)
        {
            this.initialStack = new Stack<char>(initialStack.Reverse());

            foreach (var symbol in initialStack.Reverse())
            {
                state.Stack.Push(symbol);
            }
        }
        else
        {
            state.Stack.Push(Bottom);
            this.initialStack = null;
        }

        state.IsAccepted = null;
        return state;
    }

    public override void StepForward(AutomatonExecutionState baseState)
    {
        if (baseState is not PDAExecutionState state)
            throw new ArgumentException("State must be PDAExecutionState");

        PushToHistory(state);

        var (currentInput, stackTop) = GetCurrentInputAndStackTop(state);
        LogStepForwardStart(state, currentInput, stackTop);

        var transition = FindApplicableTransition(state, currentInput, stackTop);
        LogTransitionFound(transition);

        if (transition == null)
        {
            HandleNoTransition(state);
            return;
        }

        ApplyTransition(state, transition);
        LogStepForwardEnd(state);

        if (state.Position >= state.Input.Length)
        {
            FinalizeStepAtEndOfInput(state);
        }
    }

    private static (char currentInput, char? stackTop) GetCurrentInputAndStackTop(PDAExecutionState state)
    {
        char currentInput = state.Position < state.Input.Length ? state.Input[state.Position] : '\0';
        char? stackTop = state.Stack.Count > 0 ? state.Stack.Peek() : null;
        return (currentInput, stackTop);
    }

    private Transition? FindApplicableTransition(PDAExecutionState state, char currentInput, char? stackTop)
    {
        var candidates = Transitions.Where(t => t.FromStateId == state.CurrentStateId).ToList();

        if (state.Position < state.Input.Length)
        {
            var transition = candidates.FirstOrDefault(t => t.Symbol == currentInput && StackMatches(t, stackTop));
            return transition ?? candidates.FirstOrDefault(t => t.Symbol == '\0' && StackMatches(t, stackTop));
        }

        return candidates.FirstOrDefault(t => t.Symbol == '\0' && StackMatches(t, stackTop));
    }

    private void HandleNoTransition(PDAExecutionState state)
    {
        if (state.Position < state.Input.Length)
        {
            state.Position = state.Input.Length;
            state.IsAccepted = false;
            return;
        }

        state.IsAccepted = CheckAcceptance(state.CurrentStateId, state.Stack);
    }

    private static void ApplyTransition(PDAExecutionState state, Transition transition)
    {
        if (transition.Symbol != '\0')
        {
            state.Position++;
        }

        if (!TryPopStack(state, transition))
        {
            state.Position = state.Input.Length;
            state.IsAccepted = false;
            return;
        }

        PushToStack(state, transition);
        state.CurrentStateId = transition.ToStateId;
    }

    private static bool TryPopStack(PDAExecutionState state, Transition transition)
    {
        if (!transition.StackPop.HasValue || transition.StackPop.Value == '\0')
            return true;

        if (state.Stack.Count == 0 || state.Stack.Peek() != transition.StackPop.Value)
            return false;

        state.Stack.Pop();
        return true;
    }

    private static void PushToStack(PDAExecutionState state, Transition transition)
    {
        if (string.IsNullOrEmpty(transition.StackPush))
            return;

        for (int i = transition.StackPush.Length - 1; i >= 0; i--)
        {
            state.Stack.Push(transition.StackPush[i]);
        }
    }

    private void FinalizeStepAtEndOfInput(PDAExecutionState state)
    {
        if (TryApplyEpsilonClosure(state))
        {
            state.IsAccepted = true;
            return;
        }

        state.IsAccepted = CheckAcceptance(state.CurrentStateId, state.Stack);
    }

    private static void LogStepForwardStart(PDAExecutionState state, char currentInput, char? stackTop)
    {
        try
        {
            Console.WriteLine($"PDA: StepForward pos={state.Position} inputLen={state.Input.Length} cur='{currentInput}' top='{stackTop}' state={state.CurrentStateId}");
        }
        catch { }
    }

    private static void LogTransitionFound(Transition? transition)
    {
        try
        {
            Console.WriteLine(transition == null
                ? "PDA: no transition found"
                : $"PDA: selected transition {transition.FromStateId}->{transition.ToStateId} sym='{transition.Symbol}' pop='{transition.StackPop}' push='{transition.StackPush}'");
        }
        catch { }
    }

    private static void LogStepForwardEnd(PDAExecutionState state)
    {
        try
        {
            Console.WriteLine($"PDA: after step pos={state.Position} top='{(state.Stack.Count > 0 ? state.Stack.Peek() : ' ')}' state={state.CurrentStateId} accepted={state.IsAccepted}");
        }
        catch { }
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
        if (baseState is not PDAExecutionState state)
            throw new ArgumentException("State must be PDAExecutionState");

        LogExecuteAllStart(state);

        if (state.Input.Length == 0)
        {
            HandleEmptyInput(state);
            return;
        }

        ProcessInputLoop(state);

        if (state.IsAccepted != null)
            return;

        FinalizeExecution(state);
    }

    private void HandleEmptyInput(PDAExecutionState state)
    {
        if (TryApplyEpsilonClosure(state))
        {
            state.IsAccepted = true;
            return;
        }

        state.IsAccepted = CheckAcceptance(state.CurrentStateId, state.Stack);
    }

    private void ProcessInputLoop(PDAExecutionState state)
    {
        while (true)
        {
            var hasApplicable = HasApplicableTransition(state);
            LogExecuteAllLoop(state, hasApplicable);

            if (!hasApplicable)
                break;

            StepForward(state);

            if (state.IsAccepted == false || state.IsAccepted == true)
                return;
        }
    }

    private void FinalizeExecution(PDAExecutionState state)
    {
        LogExecuteAllEnd(state);

        if (state.Position < state.Input.Length)
        {
            state.IsAccepted = false;
            return;
        }

        if (TryApplyEpsilonClosure(state))
        {
            state.IsAccepted = true;
            return;
        }

        if (TryImmediateAcceptingTransition(state))
        {
            state.IsAccepted = true;
            return;
        }

        if (CanReachAcceptingFromCurrentState(state))
        {
            state.IsAccepted = true;
            return;
        }

        state.IsAccepted = CheckAcceptance(state.CurrentStateId, state.Stack);
    }

    private bool TryImmediateAcceptingTransition(PDAExecutionState state)
    {
        if (!IsOnlyBottom(state.Stack))
            return false;

        var immediate = Transitions.FirstOrDefault(t =>
            t.FromStateId == state.CurrentStateId &&
            t.Symbol == '\0' &&
            (t.StackPop == '\0' || !t.StackPop.HasValue) &&
            string.IsNullOrEmpty(t.StackPush) &&
            IsAccepting(t.ToStateId));

        if (immediate == null)
            return false;

        state.CurrentStateId = immediate.ToStateId;
        return true;
    }

    private bool CanReachAcceptingFromCurrentState(PDAExecutionState state)
    {
        return IsOnlyBottom(state.Stack) && CanReachAcceptingViaPureEpsilons(state.CurrentStateId);
    }

    private static void LogExecuteAllStart(PDAExecutionState state)
    {
        try
        {
            Console.WriteLine($"PDA: ExecuteAll start pos={state.Position} len={state.Input.Length} state={state.CurrentStateId} top='{(state.Stack.Count > 0 ? state.Stack.Peek() : ' ')}'");
        }
        catch { }
    }

    private static void LogExecuteAllLoop(PDAExecutionState state, bool hasApplicable)
    {
        try
        {
            Console.WriteLine($"PDA: ExecuteAll loop hasApplicable={hasApplicable} pos={state.Position} state={state.CurrentStateId} top='{(state.Stack.Count > 0 ? state.Stack.Peek() : ' ')}'\n");
        }
        catch { }
    }

    private static void LogExecuteAllEnd(PDAExecutionState state)
    {
        try
        {
            Console.WriteLine($"PDA: ExecuteAll end pos={state.Position} state={state.CurrentStateId} top='{(state.Stack.Count > 0 ? state.Stack.Peek() : ' ')}' IsAccepted={state.IsAccepted}\n");
        }
        catch { }
    }

    public override void BackToStart(AutomatonExecutionState baseState)
    {
        if (baseState is not PDAExecutionState state) throw new ArgumentException("State must be PDAExecutionState");
        var start = States.FirstOrDefault(s => s.IsStart)?.Id ?? throw new InvalidOperationException("No start state defined.");
        state.CurrentStateId = start;
        state.Position = 0;
        state.Stack.Clear();

        if (initialStack != null && initialStack.Count > 0)
        {
            foreach (var symbol in initialStack.Reverse())
            {
                state.Stack.Push(symbol);
            }
        }
        else
        {
            state.Stack.Push(Bottom);
        }

        state.IsAccepted = null;
        state.History.Clear();
    }

    public new bool Execute(string input)
    {
        var state = (PDAExecutionState)StartExecution(input);

        if (!ProcessInputWithEpsilonExpansion(state))
            return false;

        return CheckFinalAcceptanceWithEpsilonClosure(state);
    }

    private bool ProcessInputWithEpsilonExpansion(PDAExecutionState state)
    {
        while (state.Position < state.Input.Length)
        {
            char currentSymbol = state.Input[state.Position];
            var stackTop = state.Stack.Count > 0 ? state.Stack.Peek() : (char?)null;
            var candidates = Transitions.Where(t => t.FromStateId == state.CurrentStateId).ToList();

            var transition = candidates.FirstOrDefault(t => t.Symbol == currentSymbol && StackMatches(t, stackTop));

            if (transition == null)
            {
                var bfsResult = FindTransitionViaEpsilonBfs(state, currentSymbol);
                if (!bfsResult.Found)
                    return false;

                state.CurrentStateId = bfsResult.StateId;
                state.Stack = new Stack<char>(bfsResult.Stack!.Reverse());
                transition = bfsResult.Transition!;
            }

            if (!ApplyTransitionToExecutionState(state, transition))
                return false;

            state.Position++;

            if (state.Position >= state.Input.Length && TryApplyEpsilonClosure(state))
                return true;
        }

        return true;
    }

    private BfsSearchResult FindTransitionViaEpsilonBfs(PDAExecutionState state, char targetSymbol)
    {
        var queue = new Queue<(int StateId, Stack<char> Stack)>();
        var visited = new HashSet<string>();
        var initialKey = CreateStateStackKey(state.CurrentStateId!.Value, state.Stack);

        queue.Enqueue((state.CurrentStateId!.Value, new Stack<char>(state.Stack.Reverse())));
        visited.Add(initialKey);

        int iterations = 0;
        var maxExpansion = Configuration.PdaExecutionSettings.MaxBfsExpansion;

        while (queue.Count > 0 && iterations++ < maxExpansion)
        {
            var (currentState, stackCopy) = queue.Dequeue();
            var stackTop = stackCopy.Count > 0 ? stackCopy.Peek() : (char?)null;

            var consumingTransition = Transitions.FirstOrDefault(t =>
                t.FromStateId == currentState &&
                t.Symbol == targetSymbol &&
                StackMatches(t, stackTop));

            if (consumingTransition != null)
            {
                return new BfsSearchResult
                {
                    Found = true,
                    StateId = currentState,
                    Stack = stackCopy,
                    Transition = consumingTransition
                };
            }

            ExpandEpsilonTransitions(queue, visited, currentState, stackCopy);
        }

        return new BfsSearchResult { Found = false };
    }

    private void ExpandEpsilonTransitions(Queue<(int StateId, Stack<char> Stack)> queue,
        HashSet<string> visited, int currentState, Stack<char> stackCopy)
    {
        var stackTop = stackCopy.Count > 0 ? stackCopy.Peek() : (char?)null;
        var epsilonTransitions = Transitions.Where(t =>
            t.FromStateId == currentState &&
            t.Symbol == '\0' &&
            StackMatches(t, stackTop));

        foreach (var transition in epsilonTransitions)
        {
            var newStack = ApplyStackOperations(stackCopy, transition);
            if (newStack == null)
                continue;

            if (newStack.Count > Configuration.PdaExecutionSettings.MaxStackGrowthTolerance)
                continue;

            var key = CreateStateStackKey(transition.ToStateId, newStack);
            if (visited.Add(key))
            {
                queue.Enqueue((transition.ToStateId, newStack));
            }
        }
    }

    private static Stack<char>? ApplyStackOperations(Stack<char> stack, Transition transition)
    {
        var newStack = new Stack<char>(stack.Reverse());

        if (transition.StackPop.HasValue && transition.StackPop.Value != '\0')
        {
            if (newStack.Count == 0 || newStack.Peek() != transition.StackPop.Value)
                return null;
            newStack.Pop();
        }

        if (!string.IsNullOrEmpty(transition.StackPush))
        {
            for (int i = transition.StackPush.Length - 1; i >= 0; i--)
                newStack.Push(transition.StackPush[i]);
        }

        return newStack;
    }

    private static bool ApplyTransitionToExecutionState(PDAExecutionState state, Transition transition)
    {
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
        return true;
    }

    private bool CheckFinalAcceptanceWithEpsilonClosure(PDAExecutionState state)
    {
        var queue = new Queue<(int StateId, string StackKey, Stack<char> Stack)>();
        var visited = new HashSet<string>();
        var initialKey = CreateStateStackKey(state.CurrentStateId!.Value, state.Stack);

        queue.Enqueue((state.CurrentStateId!.Value, CreateStackKey(state.Stack), new Stack<char>(state.Stack.Reverse())));
        visited.Add(initialKey);

        int iterations = 0;
        var maxExpansion = Configuration.PdaExecutionSettings.MaxBfsExpansion;

        while (queue.Count > 0 && iterations++ < maxExpansion)
        {
            var (currentState, _, stackCopy) = queue.Dequeue();

            if (CheckAcceptance(currentState, stackCopy))
                return true;

            ExpandEpsilonTransitionsForAcceptance(queue, visited, currentState, stackCopy);
        }

        return false;
    }

    private void ExpandEpsilonTransitionsForAcceptance(Queue<(int StateId, string StackKey, Stack<char> Stack)> queue,
        HashSet<string> visited, int currentState, Stack<char> stackCopy)
    {
        var stackTop = stackCopy.Count > 0 ? stackCopy.Peek() : (char?)null;
        var epsilonTransitions = Transitions.Where(t =>
            t.FromStateId == currentState &&
            t.Symbol == '\0' &&
            StackMatches(t, stackTop));

        foreach (var transition in epsilonTransitions)
        {
            var newStack = ApplyStackOperations(stackCopy, transition);
            if (newStack == null)
                continue;

            if (newStack.Count > Configuration.PdaExecutionSettings.MaxStackGrowthTolerance)
                continue;

            var newKey = CreateStateStackKey(transition.ToStateId, newStack);
            if (visited.Add(newKey))
            {
                queue.Enqueue((transition.ToStateId, CreateStackKey(newStack), newStack));
            }
        }
    }

    private static string CreateStateStackKey(int stateId, Stack<char> stack)
    {
        return stateId + "|" + CreateStackKey(stack);
    }

    private static string CreateStackKey(Stack<char> stack)
    {
        return new string([.. stack.Reverse()]);
    }

    private class BfsSearchResult
    {
        public bool Found { get; init; }
        public int StateId { get; init; }
        public Stack<char>? Stack { get; init; }
        public Transition? Transition { get; init; }
    }

    private bool IsAccepting(int? stateId) => stateId != null && States.Any(s => s.Id == stateId && s.IsAccepting);

    private bool CheckAcceptance(int? stateId, Stack<char> stack)
    {
        return AcceptanceMode switch
        {
            PDAAcceptanceMode.FinalStateOnly => IsAccepting(stateId),
            PDAAcceptanceMode.EmptyStackOnly => IsOnlyBottom(stack),
            PDAAcceptanceMode.FinalStateAndEmptyStack => IsAccepting(stateId) && IsOnlyBottom(stack),
            _ => IsAccepting(stateId) && IsOnlyBottom(stack) // default
        };
    }

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
            Stack = [.. state.Stack]
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
            var has = candidates.Any(t => t.Symbol == '\0' && StackMatches(t, top));
            try { Console.WriteLine($"PDA: HasApplicableTransition (input consumed) => {has}"); } catch { }
            return has;
        }
    }

    private bool TryApplyEpsilonClosure(PDAExecutionState state)
    {
        int safety = 0;
        var maxEps = Configuration.PdaExecutionSettings.MaxEpsilonIterations;
        while (safety++ < maxEps)
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

            try { Console.WriteLine($"PDA: applying epsilon {transition.FromStateId}->{transition.ToStateId} pop='{transition.StackPop}' push='{transition.StackPush}'"); } catch { }

            if (transition.StackPop.HasValue && transition.StackPop.Value != '\0')
            {
                if (state.Stack.Count == 0 || state.Stack.Peek() != transition.StackPop.Value)
                {
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

            if (CheckAcceptance(state.CurrentStateId, state.Stack))
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
        public char[] Stack { get; set; } = [];
    }
}
