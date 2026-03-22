using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using System.Collections.Immutable;

namespace FiniteAutomatons.UnitTests.FiniteAutomatons.Core.FiniteAutomataTests;

public class NPDATests
{
    private static NPDA EvenLengthPalindromeNPDA()
    {
        var pda = new NPDA();
        // State 1: Pushing first half
        // State 2: Popping second half
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        pda.AddState(new State { Id = 2, IsStart = false, IsAccepting = true });

        // Push operations in state 1
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "A" });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'b', StackPop = '\0', StackPush = "B" });

        // Nondeterministic epsilon transition to guess the middle
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 2, Symbol = '\0', StackPop = '\0', StackPush = null });

        // Pop operations in state 2
        pda.AddTransition(new Transition { FromStateId = 2, ToStateId = 2, Symbol = 'a', StackPop = 'A', StackPush = null });
        pda.AddTransition(new Transition { FromStateId = 2, ToStateId = 2, Symbol = 'b', StackPop = 'B', StackPush = null });

        pda.AddState(new State { Id = 3, IsStart = false, IsAccepting = true });
        // Pop bottom marker epsilon transition to accept via Empty Stack (for FinalStateAndEmptyStack support)
        pda.AddTransition(new Transition { FromStateId = 2, ToStateId = 3, Symbol = '\0', StackPop = '#', StackPush = "#" });


        return pda;
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("aa", true)]
    [InlineData("bb", true)]
    [InlineData("abba", true)]
    [InlineData("baab", true)]
    [InlineData("ababba", false)]
    [InlineData("a", false)]
    [InlineData("ab", false)]
    [InlineData("aba", false)]
    public void Execute_EvenLengthPalindromes(string input, bool expected)
    {
        var pda = EvenLengthPalindromeNPDA();
        pda.AcceptanceMode = PDAAcceptanceMode.FinalStateAndEmptyStack;
        pda.Execute(input).ShouldBe(expected);
    }

    [Fact]
    public void Stepwise_BranchingAndBacktracking()
    {
        var pda = EvenLengthPalindromeNPDA();
        var state = pda.StartExecution("abba");
        var npdaState = state as NPDAExecutionState;
        npdaState.ShouldNotBeNull();

        pda.StepForward(state); // consumes first 'a'
        var snapshotCount = npdaState!.Configurations.Count;
        snapshotCount.ShouldBeGreaterThan(0);

        pda.StepBackward(state);
        state.Position.ShouldBe(0);

        // Should restore correctly without mutating past bounds
        npdaState.Configurations.Count.ShouldBeGreaterThan(0);

        pda.ExecuteAll(state);
        (state.IsAccepted ?? false).ShouldBeTrue();
    }

    [Fact]
    public void AcceptanceMode_EmptyStackOnly()
    {
        var pda = new NPDA { AcceptanceMode = PDAAcceptanceMode.EmptyStackOnly };
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "A" });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'b', StackPop = 'A', StackPush = null });

        // Input "ab" pushes A then pops A, leaving just the bottom marker '#'
        pda.Execute("ab").ShouldBeTrue();
        // Input "a" leaves 'A' on the stack
        pda.Execute("a").ShouldBeFalse();
    }

    [Fact]
    public void AcceptanceMode_FinalStateOnly()
    {
        var pda = new NPDA { AcceptanceMode = PDAAcceptanceMode.FinalStateOnly };
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = true });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "A" });

        pda.Execute("a").ShouldBeTrue(); // Stack has 'A' but it's FinalStateOnly
    }

    [Fact]
    public void Serialize_And_Deserialize_NPDA_Works()
    {
        var pda = EvenLengthPalindromeNPDA();
        var json = AutomatonJsonSerializer.Serialize(pda);

        var deserialized = AutomatonJsonSerializer.Deserialize(json);
        deserialized.ShouldNotBeNull();
        deserialized.ShouldBeOfType<NPDA>();

        deserialized.Transitions.Count.ShouldBe(pda.Transitions.Count);
        deserialized.States.Count.ShouldBe(pda.States.Count);

        deserialized.Execute("abba").ShouldBeTrue();
        deserialized.Execute("aba").ShouldBeFalse();
    }

    [Fact]
    public void CustomInitialStack_SpawnsConfigurationsCorrectly()
    {
        var pda = new NPDA();
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = true });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = 'Y', StackPush = null });

        var customStack = new Stack<char>();
        customStack.Push('#');
        customStack.Push('Y');

        var state = (NPDAExecutionState)pda.StartExecution("a", customStack);
        pda.StepForward(state);

        static bool EmptyCheck(ImmutableStack<char> stack)
        {
            if (stack.IsEmpty) return false;
            if (stack.Peek() != '#') return false;
            return stack.Pop().IsEmpty;
        }

        state.Configurations.Count.ShouldBe(1);
        var resultingConfig = state.Configurations.First();
        EmptyCheck(resultingConfig.Stack).ShouldBeTrue();
    }

    [Fact]
    public void EpsilonClosure_Limits_PreventInfiniteLoop()
    {
        var pda = new NPDA();
        pda.SetLogger(NullLogger<NPDA>.Instance);
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });

        // Infinite push loop via epsilon
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = '\0', StackPop = '\0', StackPush = "X" });

        // StartExecution will try to run EpsilonClosure
        var state = pda.StartExecution("");
        var npdaState = state as NPDAExecutionState;
        npdaState.ShouldNotBeNull();

        // It shouldn't hang, it should just prune and cap configurations.
        npdaState!.Configurations.Count.ShouldBeGreaterThan(0);
        npdaState.Configurations.Count.ShouldBeLessThan(5000);

        // ExecuteAll should terminate
        pda.ExecuteAll(npdaState);
    }
}
