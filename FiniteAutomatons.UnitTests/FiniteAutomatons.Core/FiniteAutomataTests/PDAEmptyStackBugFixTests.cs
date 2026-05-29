using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using Shouldly;
using System.Collections.Immutable;

namespace FiniteAutomatons.UnitTests.FiniteAutomatons.Core.FiniteAutomataTests;

/// <summary>
/// Regression tests for the three critical PDA bugs identified in the opponent review:
///   Bug 1 – Empty stack (after # is popped) was not recognized as acceptance.
///   Bug 2 – NPDA BackToStart ignored a custom initial stack passed to StartExecution.
///   Bug 3 – Both bugs applied equally to DPDA and NPDA in FinalStateAndEmptyStack / EmptyStackOnly modes.
/// </summary>
public class PDAEmptyStackBugFixTests
{
    // ──────────────────────────────────────────────────────────────────────
    // Bug 1a: DPDA – EmptyStackOnly acceptance when # is explicitly popped
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A DPDA that reads 'a' and pops the bottom marker '#', leaving the stack
    /// completely empty. With EmptyStackOnly mode this MUST be accepted.
    /// Before the fix IsOnlyBottom returned false for an empty stack → REJECTED.
    /// </summary>
    [Fact]
    public void DPDA_EmptyStackOnly_AcceptsWhenStackBecomesTrulyEmpty()
    {
        var pda = new DPDA { AcceptanceMode = PDAAcceptanceMode.EmptyStackOnly };
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        pda.AddState(new State { Id = 2, IsStart = false, IsAccepting = false });
        // Reading 'a' pops '#' and pushes nothing → stack is now empty
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a', StackPop = '#', StackPush = null });

        pda.Execute("a").ShouldBeTrue();
    }

    /// <summary>
    /// Same DPDA as above but with FinalStateAndEmptyStack mode and an accepting target state.
    /// </summary>
    [Fact]
    public void DPDA_FinalStateAndEmptyStack_AcceptsWhenStackBecomesTrulyEmpty()
    {
        var pda = new DPDA { AcceptanceMode = PDAAcceptanceMode.FinalStateAndEmptyStack };
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        pda.AddState(new State { Id = 2, IsStart = false, IsAccepting = true });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a', StackPop = '#', StackPush = null });

        pda.Execute("a").ShouldBeTrue();
    }

    /// <summary>
    /// Ensure that a non-empty stack with something other than '#' on top still rejects.
    /// </summary>
    [Fact]
    public void DPDA_EmptyStackOnly_RejectsWhenStackIsNonEmpty()
    {
        var pda = new DPDA { AcceptanceMode = PDAAcceptanceMode.EmptyStackOnly };
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = true });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" });

        pda.Execute("a").ShouldBeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────
    // Bug 1b: NPDA – EmptyStackOnly acceptance when # is explicitly popped
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void NPDA_EmptyStackOnly_AcceptsWhenStackBecomesTrulyEmpty()
    {
        var pda = new NPDA { AcceptanceMode = PDAAcceptanceMode.EmptyStackOnly };
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        pda.AddState(new State { Id = 2, IsStart = false, IsAccepting = false });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a', StackPop = '#', StackPush = null });

        pda.Execute("a").ShouldBeTrue();
    }

    [Fact]
    public void NPDA_FinalStateAndEmptyStack_AcceptsWhenStackBecomesTrulyEmpty()
    {
        var pda = new NPDA { AcceptanceMode = PDAAcceptanceMode.FinalStateAndEmptyStack };
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        pda.AddState(new State { Id = 2, IsStart = false, IsAccepting = true });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a', StackPop = '#', StackPush = null });

        pda.Execute("a").ShouldBeTrue();
    }

    // ──────────────────────────────────────────────────────────────────────
    // Reviewer example: automaton reads 'a', removes '#', must accept "a"
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Exact repro from the review: "an automaton that after reading 'a' from the initial
    /// configuration removes '#' from the stack should accept the string 'a' with an empty
    /// stack, but the program reports REJECTED."
    /// </summary>
    [Theory]
    [InlineData(true)]   // DPDA
    [InlineData(false)]  // NPDA
    public void ReviewRepro_ReadA_RemovesBottomMarker_MustAccept(bool useDpda)
    {
        if (useDpda)
        {
            var pda = new DPDA { AcceptanceMode = PDAAcceptanceMode.EmptyStackOnly };
            pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
            pda.AddState(new State { Id = 2, IsStart = false, IsAccepting = false });
            pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a', StackPop = '#', StackPush = null });
            pda.Execute("a").ShouldBeTrue();
        }
        else
        {
            var pda = new NPDA { AcceptanceMode = PDAAcceptanceMode.EmptyStackOnly };
            pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
            pda.AddState(new State { Id = 2, IsStart = false, IsAccepting = false });
            pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a', StackPop = '#', StackPush = null });
            pda.Execute("a").ShouldBeTrue();
        }
    }

    /// <summary>
    /// A stack containing only the bottom marker '#' is treated as "effectively empty" by IsOnlyBottom.
    /// A stack still containing 'X' (above '#') must be rejected in EmptyStackOnly mode.
    /// </summary>
    [Fact]
    public void DPDA_EmptyStackOnly_RejectsWhenStackStillContainsNonBottomSymbol()
    {
        // After reading 'a': stack = [X, #] (X on top). No more input → stack not empty → reject.
        var pda = new DPDA { AcceptanceMode = PDAAcceptanceMode.EmptyStackOnly };
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" });

        pda.Execute("a").ShouldBeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────
    // Bug 2: NPDA BackToStart must restore a custom initial stack
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void NPDA_BackToStart_RestoresDefaultBottomMarkerStack()
    {
        var pda = new NPDA();
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = true });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "A" });

        var state = (NPDAExecutionState)pda.StartExecution("aa");
        pda.StepForward(state);
        pda.StepForward(state);
        state.Position.ShouldBe(2);

        pda.BackToStart(state);

        state.Position.ShouldBe(0);
        state.IsAccepted.ShouldBeNull();
        state.History.Count.ShouldBe(0);

        // Stack should be back to just '#'
        var config = state.Configurations.ShouldHaveSingleItem();
        config.Stack.IsEmpty.ShouldBeFalse();
        config.Stack.Peek().ShouldBe('#');
        config.Stack.Pop().IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void NPDA_BackToStart_RestoresCustomInitialStack()
    {
        var pda = new NPDA();
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = true });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = 'Y', StackPush = null });

        var customStack = new Stack<char>();
        customStack.Push('#');
        customStack.Push('Y'); // top

        var state = (NPDAExecutionState)pda.StartExecution("a", customStack);
        pda.StepForward(state); // consumes 'a', pops 'Y'
        state.Position.ShouldBe(1);

        pda.BackToStart(state);

        state.Position.ShouldBe(0);
        state.IsAccepted.ShouldBeNull();

        // After BackToStart the stack should have 'Y' on top (custom initial stack restored)
        var config = state.Configurations.ShouldHaveSingleItem();
        config.Stack.Peek().ShouldBe('Y');
    }

    [Fact]
    public void NPDA_BackToStart_CustomStack_ExecutionProducesConsistentResults()
    {
        // Build NPDA that needs 'Y' on top to consume 'a' and '#' to accept
        var pda = new NPDA { AcceptanceMode = PDAAcceptanceMode.EmptyStackOnly };
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        pda.AddState(new State { Id = 2, IsStart = false, IsAccepting = false });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a', StackPop = 'Y', StackPush = null });
        pda.AddTransition(new Transition { FromStateId = 2, ToStateId = 2, Symbol = 'b', StackPop = '#', StackPush = null });

        var customStack = new Stack<char>();
        customStack.Push('#');
        customStack.Push('Y');

        // First run: should accept "ab"
        var state = (NPDAExecutionState)pda.StartExecution("ab", customStack);
        pda.ExecuteAll(state);
        (state.IsAccepted ?? false).ShouldBeTrue();

        // BackToStart and run again – must give same result
        pda.BackToStart(state);
        // Re-set input by starting fresh (BackToStart only resets position/stack)
        var state2 = (NPDAExecutionState)pda.StartExecution("ab", customStack);
        pda.ExecuteAll(state2);
        (state2.IsAccepted ?? false).ShouldBeTrue();
    }

    // ──────────────────────────────────────────────────────────────────────
    // Bug 2 (DPDA variant) – verify DPDA BackToStart already worked correctly
    // (this guards against regression)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void DPDA_BackToStart_RestoresCustomInitialStack()
    {
        var pda = new DPDA { AcceptanceMode = PDAAcceptanceMode.EmptyStackOnly };
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        pda.AddState(new State { Id = 2, IsStart = false, IsAccepting = false });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a', StackPop = 'Y', StackPush = null });
        pda.AddTransition(new Transition { FromStateId = 2, ToStateId = 2, Symbol = 'b', StackPop = '#', StackPush = null });

        var customStack = new Stack<char>();
        customStack.Push('#');
        customStack.Push('Y');

        var state = (PDAExecutionState)pda.StartExecution("ab", customStack);
        pda.ExecuteAll(state);
        (state.IsAccepted ?? false).ShouldBeTrue();

        pda.BackToStart(state);
        state.Position.ShouldBe(0);
        state.Stack.Peek().ShouldBe('Y');
    }

    // ──────────────────────────────────────────────────────────────────────
    // Acceptance mode matrix – both types, all three modes, empty-stack edge
    // ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(PDAAcceptanceMode.EmptyStackOnly, true)]
    [InlineData(PDAAcceptanceMode.FinalStateAndEmptyStack, false)] // not in final state
    [InlineData(PDAAcceptanceMode.FinalStateOnly, false)]          // not in final state
    public void DPDA_AcceptanceModeMatrix_EmptyStackEdge(PDAAcceptanceMode mode, bool expected)
    {
        var pda = new DPDA { AcceptanceMode = mode };
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        pda.AddState(new State { Id = 2, IsStart = false, IsAccepting = false });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a', StackPop = '#', StackPush = null });

        pda.Execute("a").ShouldBe(expected);
    }

    [Theory]
    [InlineData(PDAAcceptanceMode.EmptyStackOnly, true)]
    [InlineData(PDAAcceptanceMode.FinalStateAndEmptyStack, false)]
    [InlineData(PDAAcceptanceMode.FinalStateOnly, false)]
    public void NPDA_AcceptanceModeMatrix_EmptyStackEdge(PDAAcceptanceMode mode, bool expected)
    {
        var pda = new NPDA { AcceptanceMode = mode };
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        pda.AddState(new State { Id = 2, IsStart = false, IsAccepting = false });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a', StackPop = '#', StackPush = null });

        pda.Execute("a").ShouldBe(expected);
    }
}
