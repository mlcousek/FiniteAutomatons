using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using Shouldly;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Services;
using Microsoft.Extensions.Logging.Abstractions;
using FiniteAutomatons.Core.Models.Serialization;

namespace FiniteAutomatons.UnitTests.FiniteAutomatons.Core.FiniteAutomataTests;

public class PDATests
{
    private static PDA SimpleParenthesesPDA()
    {
        var pda = new PDA();
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = true });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = '(', StackPop = '\0', StackPush = "(" });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = ')', StackPop = '(', StackPush = null });
        return pda;
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("()", true)]
    [InlineData("(())", true)]
    [InlineData("()()", true)]
    [InlineData("(", false)]
    [InlineData(")", false)]
    [InlineData("())", false)]
    [InlineData("(()", false)]
    public void Execute_WellFormedParentheses(string input, bool expected)
    {
        var pda = SimpleParenthesesPDA();
        pda.Execute(input).ShouldBe(expected);
    }

    [Fact]
    public void Stepwise_ParenthesesProcessing()
    {
        var pda = SimpleParenthesesPDA();
        var state = pda.StartExecution("(()())");
        pda.StepForward(state); state.CurrentStateId.ShouldBe(1); state.Position.ShouldBe(1); state.IsAccepted.ShouldBeNull();
        pda.StepForward(state); state.Position.ShouldBe(2);
        pda.StepForward(state); state.Position.ShouldBe(3);
        pda.StepForward(state); state.Position.ShouldBe(4);
        pda.StepForward(state); state.Position.ShouldBe(5);
        pda.StepForward(state); state.Position.ShouldBe(6); (state.IsAccepted ?? false).ShouldBeTrue();

        // After acceptance, stepping backward should restore previous position and clear acceptance flag
        pda.StepBackward(state); state.Position.ShouldBe(5); state.IsAccepted.ShouldBeNull();

        // Also ensure stack was restored to previous snapshot
        var pdaState = state as PDAExecutionState;
        pdaState.ShouldNotBeNull();
        // After stepping back there should still be at least the bottom marker
        pdaState.Stack.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void PDA_PushAndPopSequence()
    {
        var pda = new PDA();
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        pda.AddState(new State { Id = 2, IsStart = false, IsAccepting = true });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'b', StackPop = 'X', StackPush = null });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 2, Symbol = '\0', StackPop = '\0', StackPush = null });

        pda.Execute("aabb").ShouldBeTrue();
        pda.Execute("aaabbb").ShouldBeTrue();
        pda.Execute("ababb").ShouldBeFalse();
        pda.Execute("aaabb").ShouldBeFalse();
    }

    [Fact]
    public void StartExecution_InitialStackAndAcceptance()
    {
        var pda = SimpleParenthesesPDA();
        var state = pda.StartExecution("");
        state.CurrentStateId.ShouldBe(1);
        state.IsFinished.ShouldBeTrue();
        state.IsAccepted.ShouldBeNull();
        pda.ExecuteAll(state);
        (state.IsAccepted ?? false).ShouldBeTrue();
    }

    [Fact]
    public void ExecuteAll_LongBalancedString()
    {
        var pda = SimpleParenthesesPDA();
        var input = string.Concat(Enumerable.Repeat("(()())", 50));
        pda.Execute(input).ShouldBeTrue();
    }

    [Fact]
    public void BackToStart_ClearsHistoryAndStack()
    {
        var pda = SimpleParenthesesPDA();
        var state = pda.StartExecution("(()");
        pda.StepForward(state);
        pda.StepForward(state);
        state.Position.ShouldBe(2);
        var pdaState = state as PDAExecutionState;
        pdaState.ShouldNotBeNull();
        pdaState!.History.Count.ShouldBe(2);
        pda.BackToStart(state);
        state.Position.ShouldBe(0);
        pdaState.History.Count.ShouldBe(0);
    }

    [Fact]
    public void NoValidTransition_Rejection()
    {
        var pda = SimpleParenthesesPDA();
        var state = pda.StartExecution(")");
        pda.StepForward(state);
        (state.IsAccepted ?? true).ShouldBeFalse();
    }

    // Additional tests covering edge cases and behavior

    [Fact]
    public void EpsilonPopSequence_AllowsAcceptanceAfterPoppingStack()
    {
        // Start non-accepting, consuming 'a' pushes X; after input consumed epsilon transitions pop all X and move to accepting
        var pda = new PDA();
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        pda.AddState(new State { Id = 2, IsStart = false, IsAccepting = true });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" });
        // epsilon that pops X repeatedly
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = '\0', StackPop = 'X', StackPush = null });
        // pure epsilon to accepting (only reachable once stack reduced to bottom)
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 2, Symbol = '\0', StackPop = '\0', StackPush = null });

        // Use StartExecution + ExecuteAll to examine final state and stack
        var state = pda.StartExecution("aa");
        pda.ExecuteAll(state);
        (state.IsAccepted ?? false).ShouldBeTrue();
        var pdaState = state as PDAExecutionState;
        pdaState.ShouldNotBeNull();
        // after acceptance only bottom marker should remain
        pdaState.Stack.Count.ShouldBe(1);
    }

    [Fact]
    public void Stepwise_StackBehavior_PushAndPopVerified()
    {
        var pda = new PDA();
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'b', StackPop = 'X', StackPush = null });

        var state = pda.StartExecution("aab");
        var pdaState = state as PDAExecutionState;
        pdaState.ShouldNotBeNull();

        // initial: only bottom
        pdaState.Stack.Count.ShouldBe(1);
        pda.StepForward(state); // 'a' -> push X
        pdaState.Stack.Count.ShouldBe(2);
        pda.StepForward(state); // 'a' -> push X
        pdaState.Stack.Count.ShouldBe(3);
        pda.StepForward(state); // 'b' -> pop X
        pdaState.Stack.Count.ShouldBe(2);
    }

    [Fact]
    public void Reject_On_Pop_Mismatch_MidInput()
    {
        var pda = new PDA();
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = true });
        // transition expects to pop 'X' when reading 'b' but stack only has bottom
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'b', StackPop = 'X', StackPush = null });

        pda.Execute("b").ShouldBeFalse();
    }

    [Fact]
    public void StepBackward_RestoresStackAndPosition()
    {
        var pda = new PDA();
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" });

        var state = pda.StartExecution("aa");
        var pdaState = state as PDAExecutionState;
        pdaState.ShouldNotBeNull();

        pda.StepForward(state); // first 'a'
        pda.StepForward(state); // second 'a'
        pdaState.Stack.Count.ShouldBe(3); // bottom + 2 X
        pda.StepBackward(state);
        state.Position.ShouldBe(1);
        // after stepping back, stack count should be 2 (bottom + 1 X)
        pdaState.Stack.Count.ShouldBe(2);
    }

    // New tests requested

    [Fact]
    public void EpsilonLoopSafety_NoInfiniteLoop()
    {
        // PDA with a pure-epsilon self-loop that doesn't change the stack should not infinite loop
        var pda = new PDA();
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        // epsilon self-loop that does not pop/push
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = '\0', StackPop = '\0', StackPush = null });

        // empty input; Execute should terminate (safety limits in implementation) and return false
        pda.Execute("").ShouldBeFalse();

        // also verify ExecuteAll on execution state does not hang and results in rejection
        var state = pda.StartExecution("");
        pda.ExecuteAll(state);
        (state.IsAccepted ?? true).ShouldBeFalse();
    }

    [Fact]
    public void PopFromBottom_LeadsToRejection()
    {
        // popping the bottom marker should result in non-acceptance (stack not only-bottom)
        var pda = new PDA();
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = true });
        // transition consumes 'a' and pops bottom marker explicitly
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '#', StackPush = null });

        // executing 'a' will pop bottom; acceptance requires only-bottom, so should be rejected
        pda.Execute("a").ShouldBeFalse();
    }

    [Fact]
    public void MultiCharacterPush_PushesInCorrectOrder()
    {
        var pda = new PDA();
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        // push two characters 'X' then 'Y' as a single push string
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "XY" });

        var state = pda.StartExecution("a");
        var pdaState = state as PDAExecutionState;
        pdaState.ShouldNotBeNull();
        pda.StepForward(state);
        // Based on implementation push loop, the final top should be the first character of the string ('X')
        pdaState.Stack.Peek().ShouldBe('X');
        // and below it should be 'Y' (then bottom)
        var arr = pdaState.Stack.ToArray(); // top-first
        arr[0].ShouldBe('X');
        arr[1].ShouldBe('Y');
    }

    [Fact]
    public void Acceptance_ByStateAndStack_CombinedChecks()
    {
        // Accepting state but non-empty stack (beyond bottom) -> should be rejected
        var pda = new PDA();
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = true });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" });
        pda.Execute("a").ShouldBeFalse();

        // Non-accepting state but pure epsilon path to accepting state without stack change -> should accept
        var pda2 = new PDA();
        pda2.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        pda2.AddState(new State { Id = 2, IsStart = false, IsAccepting = true });
        pda2.AddTransition(new Transition { FromStateId = 1, ToStateId = 2, Symbol = '\0', StackPop = '\0', StackPush = null });
        pda2.Execute("").ShouldBeTrue();
    }

    [Fact]
    public void InterleavedEpsilonAndConsumingMoves_WorkMidInput()
    {
        var pda = new PDA();
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        pda.AddState(new State { Id = 2, IsStart = false, IsAccepting = true });
        // consume 'a' push X
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" });
        // epsilon move to state 2 (no stack change)
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 2, Symbol = '\0', StackPop = '\0', StackPush = null });
        // in state 2 consume 'b' and pop X
        pda.AddTransition(new Transition { FromStateId = 2, ToStateId = 2, Symbol = 'b', StackPop = 'X', StackPush = null });

        // input "ab" should be accepted: 'a' pushes X, epsilon switches to state2, 'b' pops X and state2 is accepting
        pda.Execute("ab").ShouldBeTrue();
    }

    [Fact]
    public void HistoryIntegrity_ManySteps_StepBackwardToStart()
    {
        var pda = new PDA();
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'b', StackPop = 'X', StackPush = null });

        var input = string.Concat(Enumerable.Repeat("a", 10)) + string.Concat(Enumerable.Repeat("b", 10));
        var state = pda.StartExecution(input);
        var pdaState = state as PDAExecutionState;
        pdaState.ShouldNotBeNull();

        // step forward through entire input
        for (int i = 0; i < input.Length; i++)
        {
            pda.StepForward(state);
        }

        // now step backward until start
        for (int i = input.Length - 1; i >= 0; i--)
        {
            pda.StepBackward(state);
            state.Position.ShouldBe(i);
        }

        // after backing to start, history should be empty and stack only bottom
        pdaState.History.Count.ShouldBe(0);
        pdaState.Stack.Count.ShouldBe(1);
    }

    [Fact]
    public void DeterminismEnforcement_PDAValidatorRejectsConflicts()
    {
        var validator = new AutomatonValidationService(new NullLogger<AutomatonValidationService>());
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.PDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = 'X' },
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a', StackPop = 'X' }
            ]
        };

        var (isValid, errors) = validator.ValidateAutomaton(model);
        isValid.ShouldBeFalse();
        errors.Any(e => e.Contains("PDA must be deterministic")).ShouldBeTrue();
    }

    // Serialization round-trip tests for PDA

    [Fact]
    public void Serialize_And_Deserialize_PDA_Works()
    {
        var pda = SimpleParenthesesPDA();
        var json = AutomatonJsonSerializer.Serialize(pda);

        var deserialized = AutomatonJsonSerializer.Deserialize(json);
        deserialized.ShouldNotBeNull();
        deserialized.ShouldBeOfType<PDA>();

        // preserve transitions and stack info
        deserialized.Transitions.Count.ShouldBe(pda.Transitions.Count);
        deserialized.Transitions.Any(t => t.StackPush == "(").ShouldBeTrue();
        deserialized.Transitions.Any(t => t.StackPop.HasValue && t.StackPop.Value == '(').ShouldBeTrue();

        // behavior preserved
        deserialized.Execute("(()())").ShouldBeTrue();
        deserialized.Execute("(()").ShouldBeFalse();
    }

    [Fact]
    public void Serialize_And_Deserialize_PDA_MultiCharPush_PreservesOrder()
    {
        var pda = new PDA();
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "XY" });

        var json = AutomatonJsonSerializer.Serialize(pda);
        var deserialized = AutomatonJsonSerializer.Deserialize(json) as PDA;
        deserialized.ShouldNotBeNull();

        var state = deserialized.StartExecution("a");
        var pdaState = state as PDAExecutionState;
        pdaState.ShouldNotBeNull();
        deserialized.StepForward(state);

        // top should be 'X' then 'Y'
        pdaState.Stack.Peek().ShouldBe('X');
        var arr = pdaState.Stack.ToArray();
        arr[0].ShouldBe('X');
        arr[1].ShouldBe('Y');
    }
}
