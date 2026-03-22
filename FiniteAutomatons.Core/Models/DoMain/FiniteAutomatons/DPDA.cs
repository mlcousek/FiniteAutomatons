using FiniteAutomatons.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;

public class DPDA : Automaton
{
    private const char Bottom = '#';

    private ILogger<DPDA> logger = NullLogger<DPDA>.Instance;

    public PDAAcceptanceMode AcceptanceMode { get; set; } = PDAAcceptanceMode.FinalStateAndEmptyStack;

    private IReadOnlyList<char>? initialStackSymbols;

    public void SetLogger(ILogger<DPDA> value) => logger = value;

    public override AutomatonExecutionState StartExecution(string input, Stack<char>? initialStack = null)
    {
        var startId = ValidateStartState();
        ValidateDeterminism();
        var state = new PDAExecutionState(input, startId)
        {
            Stack = new Stack<char>()
        };

        if (initialStack is { Count: > 0 })
        {
            var symbols = initialStack.Reverse().ToList();
            initialStackSymbols = symbols;
            foreach (var sym in symbols)
                state.Stack.Push(sym);
        }
        else
        {
            initialStackSymbols = null;
            state.Stack.Push(Bottom);
        }

        state.IsAccepted = null;
        return state;
    }

    public override void StepForward(AutomatonExecutionState baseState)
    {
        if (baseState is not PDAExecutionState state)
            throw new ArgumentException("DPDA requires a PDAExecutionState.", nameof(baseState));

        if (state.IsAccepted.HasValue)
        {
            return;
        }

        PushSnapshot(state);

        var (inputSymbol, stackTop) = ReadHead(state);
        logger.LogDebug("DPDA StepForward: pos={Position} input='{Symbol}' top='{Top}' state={State}",
            state.Position, inputSymbol == '\0' ? 'ε' : inputSymbol, stackTop?.ToString() ?? "∅", state.CurrentStateId);

        if (TryApplyDeterministicEpsilonChain(state))
        {
            (_, stackTop) = ReadHead(state);
        }

        if (state.IsAccepted.HasValue)
            return;

        if (state.Position < state.Input.Length)
        {
            char symbol = state.Input[state.Position];
            var transition = FindConsumingTransition(state.CurrentStateId!.Value, symbol, stackTop);
            if (transition is null)
            {
                logger.LogDebug("DPDA StepForward: no transition found → reject");
                state.Position = state.Input.Length;
                state.IsAccepted = false;
                return;
            }

            ApplyTransition(state, transition, consume: true);
            logger.LogDebug("DPDA StepForward: applied {From}→{To} sym='{Sym}'",
                transition.FromStateId, transition.ToStateId, symbol);

            if (state.Position >= state.Input.Length)
                EvaluateAcceptance(state);
        }
        else
        {
            TryApplyDeterministicEpsilonChain(state);
            if (!state.IsAccepted.HasValue)
                state.IsAccepted = CheckAcceptance(state.CurrentStateId, state.Stack);
        }
    }

    public override void StepBackward(AutomatonExecutionState baseState)
    {
        if (baseState is not PDAExecutionState state)
            throw new ArgumentException("DPDA requires a PDAExecutionState.", nameof(baseState));

        if (state.History.Count == 0)
            return;

        var snap = state.History.Pop();
        state.CurrentStateId = snap.StateId;
        state.Position = snap.Position;
        state.Stack = new Stack<char>(snap.Stack.Reverse());
        state.IsAccepted = null;
    }

    public override void ExecuteAll(AutomatonExecutionState baseState)
    {
        if (baseState is not PDAExecutionState state)
            throw new ArgumentException("DPDA requires a PDAExecutionState.", nameof(baseState));

        ValidateDeterminism();
        logger.LogDebug("DPDA ExecuteAll: input='{Input}' len={Length}", state.Input, state.Input.Length);

        TryApplyDeterministicEpsilonChain(state);

        if (state.IsAccepted.HasValue)
            return;

        if (state.Input.Length == 0)
        {
            state.IsAccepted = CheckAcceptance(state.CurrentStateId, state.Stack);
            return;
        }

        // Main loop: consume each symbol.
        while (state.Position < state.Input.Length)
        {
            var (_, stackTop) = ReadHead(state);
            char symbol = state.Input[state.Position];

            var transition = FindConsumingTransition(state.CurrentStateId!.Value, symbol, stackTop);
            if (transition is null)
            {
                logger.LogDebug("DPDA ExecuteAll: no consuming transition at pos={Position} → reject", state.Position);
                state.IsAccepted = false;
                return;
            }

            ApplyTransition(state, transition, consume: true);

            // After consuming, apply epsilon closure.
            TryApplyDeterministicEpsilonChain(state);

            if (state.IsAccepted.HasValue)
                return;
        }

        // All input consumed — evaluate acceptance.
        state.IsAccepted = CheckAcceptance(state.CurrentStateId, state.Stack);
        logger.LogDebug("DPDA ExecuteAll: finished pos={Position} state={State} accepted={Accepted}",
            state.Position, state.CurrentStateId, state.IsAccepted);
    }

    public override void BackToStart(AutomatonExecutionState baseState)
    {
        if (baseState is not PDAExecutionState state)
            throw new ArgumentException("DPDA requires a PDAExecutionState.", nameof(baseState));

        var startId = States.FirstOrDefault(s => s.IsStart)?.Id
            ?? throw new InvalidOperationException("No start state defined.");

        state.CurrentStateId = startId;
        state.Position = 0;
        state.IsAccepted = null;
        state.History.Clear();

        state.Stack.Clear();
        if (initialStackSymbols is { Count: > 0 })
        {
            foreach (var sym in initialStackSymbols)
                state.Stack.Push(sym);
        }
        else
        {
            state.Stack.Push(Bottom);
        }
    }

    // ──────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────

    private static (char inputSymbol, char? stackTop) ReadHead(PDAExecutionState state)
    {
        char inputSym = state.Position < state.Input.Length ? state.Input[state.Position] : '\0';
        char? stackTop = state.Stack.Count > 0 ? state.Stack.Peek() : null;
        return (inputSym, stackTop);
    }

    private bool TryApplyDeterministicEpsilonChain(PDAExecutionState state)
    {
        bool applied = false;
        int safety = 0;
        int limit = PdaSettings.MaxEpsilonIterations;

        while (safety++ < limit)
        {
            var stackTop = state.Stack.Count > 0 ? state.Stack.Peek() : (char?)null;
            var eps = Transitions.FirstOrDefault(t =>
                t.FromStateId == state.CurrentStateId &&
                t.Symbol == '\0' &&
                StackMatches(t, stackTop));

            if (eps is null)
                break;

            if (!TryPopStack(state.Stack, eps))
                break; // Stack precondition not met → no epsilon chain possible.

            PushToStack(state.Stack, eps);
            state.CurrentStateId = eps.ToStateId;
            applied = true;

            logger.LogDebug("DPDA epsilon: {From}→{To} pop='{Pop}' push='{Push}'",
                eps.FromStateId, eps.ToStateId, eps.StackPop, eps.StackPush);

            // Only evaluate acceptance if we have consumed all input
            if (state.Position >= state.Input.Length)
            {
                if (CheckAcceptance(state.CurrentStateId, state.Stack))
                {
                    state.IsAccepted = true;
                    return applied;
                }
            }
        }

        return applied;
    }

    private Transition? FindConsumingTransition(int stateId, char symbol, char? stackTop)
        => Transitions.FirstOrDefault(t =>
            t.FromStateId == stateId &&
            t.Symbol == symbol &&
            StackMatches(t, stackTop));

    private void EvaluateAcceptance(PDAExecutionState state)
    {
        // After the last symbol is consumed, apply any trailing epsilon chain.
        TryApplyDeterministicEpsilonChain(state);
        if (!state.IsAccepted.HasValue)
            state.IsAccepted = CheckAcceptance(state.CurrentStateId, state.Stack);
    }

    private static void ApplyTransition(PDAExecutionState state, Transition transition, bool consume)
    {
        if (consume)
            state.Position++;

        TryPopStack(state.Stack, transition); // validity already ensured by FindConsumingTransition
        PushToStack(state.Stack, transition);
        state.CurrentStateId = transition.ToStateId;
    }

    private static bool TryPopStack(Stack<char> stack, Transition transition)
    {
        if (!transition.StackPop.HasValue || transition.StackPop.Value == '\0')
            return true;

        if (stack.Count == 0 || stack.Peek() != transition.StackPop.Value)
            return false;

        stack.Pop();
        return true;
    }

    private static void PushToStack(Stack<char> stack, Transition transition)
    {
        if (string.IsNullOrEmpty(transition.StackPush))
            return;

        // Push from right-to-left so StackPush[0] ends up on top.
        for (int i = transition.StackPush.Length - 1; i >= 0; i--)
            stack.Push(transition.StackPush[i]);
    }

    private bool CheckAcceptance(int? stateId, Stack<char> stack)
    {
        return AcceptanceMode switch
        {
            PDAAcceptanceMode.FinalStateOnly => IsAccepting(stateId),
            PDAAcceptanceMode.EmptyStackOnly => IsOnlyBottom(stack),
            PDAAcceptanceMode.FinalStateAndEmptyStack => IsAccepting(stateId) && IsOnlyBottom(stack),
            _ => IsAccepting(stateId) && IsOnlyBottom(stack)
        };
    }

    private bool IsAccepting(int? stateId)
        => stateId != null && States.Any(s => s.Id == stateId && s.IsAccepting);

    private static bool IsOnlyBottom(Stack<char> stack)
        => stack.Count == 1 && stack.Peek() == Bottom;

    private void ValidateDeterminism()
    {
        for (int i = 0; i < Transitions.Count; i++)
        {
            for (int j = i + 1; j < Transitions.Count; j++)
            {
                var t1 = Transitions[i];
                var t2 = Transitions[j];
                if (t1.FromStateId != t2.FromStateId) continue;
                if (!StackConditionsOverlap(t1, t2)) continue;
                bool t1IsEpsilon = t1.Symbol == '\0';
                bool t2IsEpsilon = t2.Symbol == '\0';
                if (t1.Symbol == t2.Symbol)
                    throw new InvalidOperationException(
                        $"DPDA determinism violated: state {t1.FromStateId} has two transitions " +
                        $"on '{(t1IsEpsilon ? 'ε' : t1.Symbol)}' with overlapping stack conditions. Use NPDA for nondeterministic automata.");
                if (t1IsEpsilon ^ t2IsEpsilon)
                    throw new InvalidOperationException(
                        $"DPDA determinism violated: state {t1.FromStateId} has both an ε-transition " +
                        $"and a consuming transition on '{(t1IsEpsilon ? t2.Symbol : t1.Symbol)}' " +
                        $"with overlapping stack conditions. Use NPDA for nondeterministic automata.");
            }
        }
    }

    private static bool StackConditionsOverlap(Transition t1, Transition t2)
    {
        bool t1AnyTop = !t1.StackPop.HasValue || t1.StackPop.Value == '\0';
        bool t2AnyTop = !t2.StackPop.HasValue || t2.StackPop.Value == '\0';
        if (t1AnyTop || t2AnyTop) return true;
        return t1.StackPop!.Value == t2.StackPop!.Value;
    }

    private static bool StackMatches(Transition t, char? top)
    {
        if (!t.StackPop.HasValue) return true;
        if (t.StackPop.Value == '\0') return true;
        return top.HasValue && top.Value == t.StackPop.Value;
    }

    private static void PushSnapshot(PDAExecutionState state)
    {
        state.History.Push(new PDAExecutionState.Snapshot
        {
            StateId = state.CurrentStateId,
            Position = state.Position,
            Stack = [.. state.Stack]
        });
    }
}
