using FiniteAutomatons.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Immutable;

namespace FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;

public class NPDA : Automaton
{
    private const char Bottom = '#';

    private ILogger<NPDA> logger = NullLogger<NPDA>.Instance;

    public PDAAcceptanceMode AcceptanceMode { get; set; } = PDAAcceptanceMode.FinalStateAndEmptyStack;

    public void SetLogger(ILogger<NPDA> value) => logger = value;

    public override AutomatonExecutionState StartExecution(string input, Stack<char>? initialStack = null)
    {
        var startId = ValidateStartState();

        ImmutableStack<char> stack;
        if (initialStack is { Count: > 0 })
        {
            stack = ImmutableStack<char>.Empty;
            foreach (var sym in initialStack.Reverse())
                stack = stack.Push(sym);
        }
        else
        {
            stack = ImmutableStack<char>.Empty.Push(Bottom);
        }

        var initialConfig = new PDAConfiguration(startId, stack);
        var state = new NPDAExecutionState(input, startId, Bottom)
        {
            Configurations = EpsilonClosure([initialConfig])
        };

        if (input.Length == 0)
            state.IsAccepted = EvaluateAcceptance(state.Configurations);

        return state;
    }

    public override void StepForward(AutomatonExecutionState baseState)
    {
        if (baseState is not NPDAExecutionState state)
            throw new ArgumentException("NPDA requires an NPDAExecutionState.", nameof(baseState));

        if (state.IsAccepted.HasValue || state.IsDead)
            return;

        if (state.Position >= state.Input.Length)
        {
            state.IsAccepted = EvaluateAcceptance(state.Configurations);
            return;
        }

        state.History.Push([.. state.Configurations]);

        char symbol = state.Input[state.Position];

        logger.LogDebug("NPDA StepForward: pos={Position} symbol='{Symbol}' activeConfigs={Count}",
            state.Position, symbol, state.Configurations.Count);

        var successors = new HashSet<PDAConfiguration>();
        foreach (var config in state.Configurations)
        {
            var stackTop = config.Stack.IsEmpty ? (char?)null : config.Stack.Peek();
            foreach (var t in Transitions.Where(t =>
                t.FromStateId == config.StateId &&
                t.Symbol == symbol &&
                StackMatches(t, stackTop)))
            {
                var newStack = ApplyStackOp(config.Stack, t);
                if (newStack is not null)
                    successors.Add(new PDAConfiguration(t.ToStateId, newStack));
            }
        }

        var afterClosure = EpsilonClosure(successors);

        state.Configurations = afterClosure;
        state.Position++;

        logger.LogDebug("NPDA StepForward: after step activeConfigs={Count}", state.Configurations.Count);

        if (state.Position >= state.Input.Length)
            state.IsAccepted = EvaluateAcceptance(state.Configurations);
    }

    public override void StepBackward(AutomatonExecutionState baseState)
    {
        if (baseState is not NPDAExecutionState state)
            throw new ArgumentException("NPDA requires an NPDAExecutionState.", nameof(baseState));

        if (state.History.Count == 0)
            return;

        state.Configurations = state.History.Pop();
        state.Position--;
        state.IsAccepted = null;
    }

    public override void ExecuteAll(AutomatonExecutionState baseState)
    {
        if (baseState is not NPDAExecutionState state)
            throw new ArgumentException("NPDA requires an NPDAExecutionState.", nameof(baseState));

        while (!state.IsFinished && !state.IsAccepted.HasValue && !state.IsDead)
            StepForward(state);

        if (!state.IsAccepted.HasValue)
            state.IsAccepted = EvaluateAcceptance(state.Configurations);
    }

    public override void BackToStart(AutomatonExecutionState baseState)
    {
        if (baseState is not NPDAExecutionState state)
            throw new ArgumentException("NPDA requires an NPDAExecutionState.", nameof(baseState));

        var startId = States.FirstOrDefault(s => s.IsStart)?.Id
            ?? throw new InvalidOperationException("No start state defined.");

        state.Position = 0;
        state.IsAccepted = null;
        state.History.Clear();

        var initialStack = ImmutableStack<char>.Empty.Push(Bottom);
        var initialConfig = new PDAConfiguration(startId, initialStack);
        state.Configurations = EpsilonClosure([initialConfig]);
    }

    internal HashSet<PDAConfiguration> EpsilonClosure(IEnumerable<PDAConfiguration> seed)
    {
        var result = new HashSet<PDAConfiguration>(seed, ConfigurationKeyComparer.Instance);
        var queue = new Queue<PDAConfiguration>(result);
        int iterations = 0;
        int limit = PdaSettings.MaxBfsExpansion;

        while (queue.Count > 0 && iterations++ < limit)
        {
            var config = queue.Dequeue();
            var stackTop = config.Stack.IsEmpty ? (char?)null : config.Stack.Peek();

            foreach (var t in Transitions.Where(t =>
                t.FromStateId == config.StateId &&
                t.Symbol == '\0' &&
                StackMatches(t, stackTop)))
            {
                var newStack = ApplyStackOp(config.Stack, t);
                if (newStack is null)
                    continue;

                if (newStack.Count() > PdaSettings.MaxStackGrowthTolerance)
                    continue;

                var successor = new PDAConfiguration(t.ToStateId, newStack);
                if (result.Add(successor))
                    queue.Enqueue(successor);
            }
        }

        if (iterations >= limit)
            logger.LogWarning("NPDA EpsilonClosure reached BFS expansion limit ({Limit}). " +
                "Some configurations may be pruned.", limit);

        return result;
    }

    private bool EvaluateAcceptance(HashSet<PDAConfiguration> configs)
        => configs.Any(IsAcceptingConfig);

    private bool IsAcceptingConfig(PDAConfiguration config)
    {
        return AcceptanceMode switch
        {
            PDAAcceptanceMode.FinalStateOnly =>
                IsAcceptingState(config.StateId),
            PDAAcceptanceMode.EmptyStackOnly =>
                IsOnlyBottom(config.Stack),
            PDAAcceptanceMode.FinalStateAndEmptyStack =>
                IsAcceptingState(config.StateId) && IsOnlyBottom(config.Stack),
            _ => IsAcceptingState(config.StateId) && IsOnlyBottom(config.Stack)
        };
    }

    private bool IsAcceptingState(int stateId)
        => States.Any(s => s.Id == stateId && s.IsAccepting);

    private static bool IsOnlyBottom(ImmutableStack<char> stack)
    {
        if (stack.IsEmpty) return false;
        var top = stack.Peek();
        if (top != Bottom) return false;
        return stack.Pop().IsEmpty; // only one element
    }

    private static ImmutableStack<char>? ApplyStackOp(ImmutableStack<char> stack, Transition t)
    {
        var result = stack;

        // Pop
        if (t.StackPop.HasValue && t.StackPop.Value != '\0')
        {
            if (result.IsEmpty || result.Peek() != t.StackPop.Value)
                return null;
            result = result.Pop();
        }

        // Push (left-to-right → first char ends up on top)
        if (!string.IsNullOrEmpty(t.StackPush))
        {
            for (int i = t.StackPush.Length - 1; i >= 0; i--)
                result = result.Push(t.StackPush[i]);
        }

        return result;
    }

    private static bool StackMatches(Transition t, char? top)
    {
        if (!t.StackPop.HasValue) return true;
        if (t.StackPop.Value == '\0') return true;
        return top.HasValue && top.Value == t.StackPop.Value;
    }

    private sealed class ConfigurationKeyComparer : IEqualityComparer<PDAConfiguration>
    {
        public static readonly ConfigurationKeyComparer Instance = new();

        public bool Equals(PDAConfiguration? x, PDAConfiguration? y)
        {
            if (x is null && y is null) return true;
            if (x is null || y is null) return false;
            return x.Key == y.Key;
        }

        public int GetHashCode(PDAConfiguration obj)
            => obj.Key.GetHashCode(StringComparison.Ordinal);
    }
}
