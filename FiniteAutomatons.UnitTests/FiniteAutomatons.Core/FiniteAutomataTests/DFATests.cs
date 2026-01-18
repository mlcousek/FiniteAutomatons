using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using Shouldly;

namespace FiniteAutomatons.UnitTests.FiniteAutomatons.Core.FiniteAutomataTests;

public class DFATests  //TODO implement this builder to other tests
{

    [Fact]
    public void Execute_ValidInput_ShouldReturnTrue()
    {
        // Arrange
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        // Act
        var result = dfa.Execute("a");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Execute_InvalidInput_ShouldReturnFalse()
    {
        // Arrange
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        // Act
        var result = dfa.Execute("b");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Execute_NoStartState_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var dfa = new DFABuilder()
            .WithState(1, isStart: false, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => dfa.Execute("a"));
    }

    [Fact]
    public void Execute_EmptyInput_ShouldReturnFalse()
    {
        // Arrange
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        // Act
        var result = dfa.Execute("");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Execute_EndInNonAcceptingState_ShouldReturnFalse()
    {
        // Arrange
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: false)
            .WithState(3, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .WithTransition(2, 3, 'b')
            .Build();

        // Act
        var result = dfa.Execute("a");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Execute_ComplexInput_ShouldReturnTrue()
    {
        // Arrange
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: false)
            .WithState(3, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .WithTransition(2, 3, 'b')
            .Build();

        // Act
        var result = dfa.Execute("ab"); // Should follow the path 1->2->3

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Execute_MultipleTransitions_ShouldFollowCorrectPath()
    {
        // Arrange
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: false)
            .WithState(3, isStart: false, isAccepting: true)
            .WithState(4, isStart: false, isAccepting: false)
            .WithTransition(1, 2, 'a')
            .WithTransition(2, 3, 'b')
            .WithTransition(2, 4, 'c')
            .Build();

        // Act & Assert
        dfa.Execute("ab").ShouldBeTrue();  // Path to accepting state
        dfa.Execute("ac").ShouldBeFalse(); // Path to non-accepting state
    }

    [Fact]
    public void Execute_StartStateAlsoAccepting_EmptyInputShouldReturnTrue()
    {
        // Arrange
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: true)
            .WithState(2, isStart: false, isAccepting: false)
            .WithTransition(1, 2, 'a')
            .Build();

        // Act
        var result = dfa.Execute("");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Execute_MultipleSymbolsWithSameTransition_ShouldWorkCorrectly()
    {
        // Arrange
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 1, 'a') // Self-loop on 'a'
            .WithTransition(1, 2, 'b')
            .Build();

        // Act & Assert
        dfa.Execute("aaaab").ShouldBeTrue();
        dfa.Execute("aaaaa").ShouldBeFalse();
    }

    [Theory]
    [InlineData("ab", true)]
    [InlineData("ac", false)]
    [InlineData("abc", false)]
    [InlineData("", false)]
    public void Execute_VariousInputs_ShouldReturnExpectedResults(string input, bool expected)
    {
        // Arrange
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: false)
            .WithState(3, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .WithTransition(2, 3, 'b')
            .Build();

        // Act
        var result = dfa.Execute(input);

        // Assert
        result.ShouldBe(expected);
    }


    [Fact]
    public void ExecuteAll_ProcessesEntireInputAndSetsIsAccepted()
    {
        // Arrange
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        var state = dfa.StartExecution("a");

        // Act
        dfa.ExecuteAll(state);

        // Assert
        state.CurrentStateId.ShouldBe(2);
        state.Position.ShouldBe(1);
        state.IsAccepted.ShouldBe(true);
    }

    [Fact]
    public void ExecuteAll_EmptyInput_SetsIsAcceptedBasedOnStartState()
    {
        // Arrange
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: true)
            .Build();

        var state = dfa.StartExecution("");

        // Act
        dfa.ExecuteAll(state);

        // Assert
        state.CurrentStateId.ShouldBe(1);
        state.Position.ShouldBe(0);
        state.IsAccepted.ShouldBe(true);
    }

    [Fact]
    public void StepForward_ValidTransition_UpdatesStateAndPosition()
    {
        // Arrange
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        var state = dfa.StartExecution("a");

        // Act
        dfa.StepForward(state);

        // Assert
        state.CurrentStateId.ShouldBe(2);
        state.Position.ShouldBe(1);
        state.IsAccepted.ShouldBe(true);
    }

    [Fact]
    public void StepForward_NoValidTransition_SetsIsAcceptedFalseAndFinishes()
    {
        // Arrange
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        var state = dfa.StartExecution("b");

        // Act
        dfa.StepForward(state);

        // Assert
        state.IsAccepted.ShouldBe(false);
        state.Position.ShouldBe(1); // Marked as finished
    }

    [Fact]
    public void StepForward_AtEndOfInput_SetsIsAcceptedBasedOnCurrentState()
    {
        // Arrange
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        var state = dfa.StartExecution("a");
        dfa.StepForward(state); // Move to state 2, position 1

        // Act
        dfa.StepForward(state); // Should check acceptance

        // Assert
        state.IsAccepted.ShouldBe(true);
    }

    [Fact]
    public void StartExecution_WithValidStartState_ShouldInitializeStateCorrectly()
    {
        // Arrange
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        // Act
        var execState = dfa.StartExecution("a");

        // Assert
        execState.CurrentStateId.ShouldBe(1);
        execState.Input.ShouldBe("a");
        execState.Position.ShouldBe(0);
        execState.IsAccepted.ShouldBeNull();
        execState.IsFinished.ShouldBeFalse();
    }

    [Fact]
    public void StartExecution_NoStartState_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var dfa = new DFABuilder()
            .WithState(1, isStart: false, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .Build();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => dfa.StartExecution("a"));
    }

    [Fact]
    public void StartExecution_WithEmptyInput_ShouldInitializeStateCorrectly()
    {
        // Arrange
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: true)
            .Build();

        // Act
        var execState = dfa.StartExecution("");

        // Assert
        execState.CurrentStateId.ShouldBe(1);
        execState.Input.ShouldBe("");
        execState.Position.ShouldBe(0);
        execState.IsAccepted.ShouldBeNull();
        execState.IsFinished.ShouldBeTrue();
    }

    [Fact]
    public void StepBackward_FromMiddleOfInput_ShouldMoveBackOneStep()
    {
        // Arrange
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .WithTransition(2, 2, 'b')
            .Build();

        var state = dfa.StartExecution("abb");
        dfa.StepForward(state); // 'a' -> state 2, pos 1
        dfa.StepForward(state); // 'b' -> state 2, pos 2

        // Act
        dfa.StepBackward(state);

        // Assert
        state.Position.ShouldBe(1);
        state.CurrentStateId.ShouldBe(2);
        state.IsAccepted.ShouldBeNull();
    }


    [Fact]
    public void StepBackward_AtStartOfInput_ShouldDoNothing()
    {
        // Arrange
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        var state = dfa.StartExecution("a");

        // Act
        dfa.StepBackward(state);

        // Assert
        state.Position.ShouldBe(0);
        state.CurrentStateId.ShouldBe(1);
        state.IsAccepted.ShouldBeNull();
    }

    [Fact]
    public void StepBackward_AfterStepForward_ShouldReturnToStartState()
    {
        // Arrange
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        var state = dfa.StartExecution("a");
        dfa.StepForward(state); // Move to state 2, pos 1

        // Act
        dfa.StepBackward(state);

        // Assert
        state.Position.ShouldBe(0);
        state.CurrentStateId.ShouldBe(1);
        state.IsAccepted.ShouldBeNull();
    }

    [Fact]
    public void StepBackward_AfterInvalidTransition_ShouldSetCurrentStateIdNull()
    {
        // Arrange
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        var state = dfa.StartExecution("ab");
        dfa.StepForward(state); // 'a' -> state 2, pos 1
        dfa.StepForward(state); // 'b' -> no transition, pos 2, IsAccepted = false

        // Act
        dfa.StepBackward(state); // Should go back to pos 1, state 2

        // Assert
        state.Position.ShouldBe(1);
        state.CurrentStateId.ShouldBe(2);
        state.IsAccepted.ShouldBeNull();
    }

    [Fact]
    public void StepBackward_ToStartState_ShouldSetCurrentStateIdToStart()
    {
        // Arrange
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: true)
            .WithState(2, isStart: false, isAccepting: false)
            .WithTransition(1, 2, 'a')
            .Build();

        var state = dfa.StartExecution("a");
        dfa.StepForward(state); // Move to state 2, pos 1

        // Act
        dfa.StepBackward(state); // Back to start

        // Assert
        state.Position.ShouldBe(0);
        state.CurrentStateId.ShouldBe(1);
        state.IsAccepted.ShouldBeNull();
    }

    [Fact]
    public void StepForward_PushesStateToHistory()
    {
        // Arrange
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        var state = dfa.StartExecution("a");

        // Act
        dfa.StepForward(state);

        // Assert
        state.StateHistory.Count.ShouldBe(1);
        state.StateHistory.Peek().ShouldContain(1); // The start state was pushed
    }

    [Fact]
    public void StepBackward_PopsStateFromHistory_AndRestoresPreviousState()
    {
        // Arrange
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        var state = dfa.StartExecution("a");
        dfa.StepForward(state); // Move to state 2, pos 1

        // Act
        dfa.StepBackward(state); // Should restore to state 1

        // Assert
        state.CurrentStateId.ShouldBe(1);
        state.StateHistory.Count.ShouldBe(0);
    }

    [Fact]
    public void StepForward_And_StepBackward_MultipleSteps_ManageHistoryCorrectly()
    {
        // Arrange
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: false)
            .WithState(3, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .WithTransition(2, 3, 'b')
            .Build();

        var state = dfa.StartExecution("ab");
        dfa.StepForward(state); // 'a' -> 2
        dfa.StepForward(state); // 'b' -> 3

        // Assert history after two steps
        state.StateHistory.Count.ShouldBe(2);
        state.StateHistory.ToArray()[1].ShouldContain(1); // First pushed state
        state.StateHistory.ToArray()[0].ShouldContain(2); // Second pushed state

        // Act: Step backward twice
        dfa.StepBackward(state); // Should restore to state 2
        state.CurrentStateId.ShouldBe(2);
        state.StateHistory.Count.ShouldBe(1);

        dfa.StepBackward(state); // Should restore to state 1
        state.CurrentStateId.ShouldBe(1);
        state.StateHistory.Count.ShouldBe(0);
    }

    [Fact]
    public void BackToStart_ResetsStateToInitialConditions()
    {
        // Arrange
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        var state = dfa.StartExecution("a");
        dfa.StepForward(state); // Move to state 2, pos 1
        state.IsAccepted = true; // Simulate acceptance
        state.StateHistory.Push([1]); // Simulate history

        // Act
        dfa.BackToStart(state);

        // Assert
        state.Position.ShouldBe(0);
        state.CurrentStateId.ShouldBe(1);
        state.IsAccepted.ShouldBeNull();
        state.StateHistory.Count.ShouldBe(0);
    }

    [Fact]
    public void DFA_FullWorkflow_ComplexScenario()
    {
        // Arrange: DFA for language accepting "ab", "abc", "aabbc"
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: false)
            .WithState(3, isStart: false, isAccepting: true)
            .WithState(4, isStart: false, isAccepting: false)
            .WithTransition(1, 2, 'a')
            .WithTransition(2, 2, 'a') // allow multiple 'a's
            .WithTransition(2, 3, 'b')
            .WithTransition(3, 4, 'c')
            .Build();

        // Start execution with input "aabc"
        var state = dfa.StartExecution("aabc");

        // Step 1: 'a' -> state 2
        dfa.StepForward(state);
        state.CurrentStateId.ShouldBe(2);
        state.Position.ShouldBe(1);
        state.IsAccepted.ShouldBeNull();

        // Step 2: 'a' -> state 2 (self-loop)
        dfa.StepForward(state);
        state.CurrentStateId.ShouldBe(2);
        state.Position.ShouldBe(2);
        state.IsAccepted.ShouldBeNull();

        // Step 3: 'b' -> state 3 (accepting)
        dfa.StepForward(state);
        state.CurrentStateId.ShouldBe(3);
        state.Position.ShouldBe(3);
        state.IsAccepted.ShouldBeNull();

        // Step 4: 'c' -> state 4 (not accepting)
        dfa.StepForward(state);
        state.CurrentStateId.ShouldBe(4);
        state.Position.ShouldBe(4);
        // At end of input, check acceptance
        state.IsAccepted.ShouldBe(false);

        // Step backward: back to state 3 (accepting)
        dfa.StepBackward(state);
        state.CurrentStateId.ShouldBe(3);
        state.Position.ShouldBe(3);
        state.IsAccepted.ShouldBeNull();

        // Step backward: back to state 2
        dfa.StepBackward(state);
        state.CurrentStateId.ShouldBe(2);
        state.Position.ShouldBe(2);
        state.IsAccepted.ShouldBeNull();

        // Step forward again: 'b' -> state 3
        dfa.StepForward(state);
        state.CurrentStateId.ShouldBe(3);
        state.Position.ShouldBe(3);

        // ExecuteAll from current position (should finish at state 4, not accepting)
        dfa.ExecuteAll(state);
        state.CurrentStateId.ShouldBe(4);
        state.Position.ShouldBe(4);
        state.IsAccepted.ShouldBe(false);

        // Back to start
        dfa.BackToStart(state);
        state.Position.ShouldBe(0);
        state.CurrentStateId.ShouldBe(1);
        state.IsAccepted.ShouldBeNull();
        state.StateHistory.Count.ShouldBe(0);

        // ExecuteAll from start (should finish at state 4, not accepting)
        dfa.ExecuteAll(state);
        state.CurrentStateId.ShouldBe(4);
        state.Position.ShouldBe(4);
        state.IsAccepted.ShouldBe(false);
    }

    [Fact]
    public void MinimalizeDFA_RemovesEquivalentStates_SimpleCase()
    {
        // Arrange: DFA with two equivalent non-accepting states (1 and 2)
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: false)
            .WithState(3, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .WithTransition(2, 3, 'b')
            .WithTransition(1, 3, 'b')
            .WithTransition(2, 2, 'a')
            .WithTransition(3, 3, 'a')
            .WithTransition(3, 3, 'b')
            .Build();

        // Act
        var minimized = dfa.MinimalizeDFA();

        // Assert: Should have only 3 states (merging 1 and 2 is not possible, but unreachable states are removed)
        minimized.States.Count.ShouldBe(2);

        minimized.Execute("ab").ShouldBeTrue();
        minimized.Execute("b").ShouldBeTrue();
        minimized.Execute("aab").ShouldBeTrue();
        minimized.Execute("a").ShouldBeFalse();
        minimized.Execute("ba").ShouldBeTrue();
    }

    [Fact]
    public void MinimalizeDFA_MergesEquivalentAcceptingStates()
    {
        // Arrange: DFA with two equivalent accepting states (2 and 3)
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithState(3, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .WithTransition(1, 3, 'b')
            .WithTransition(2, 2, 'a')
            .WithTransition(2, 2, 'b')
            .WithTransition(3, 3, 'a')
            .WithTransition(3, 3, 'b')
            .Build();

        // Act
        var minimized = dfa.MinimalizeDFA();

        // Assert: Only two states should remain (start and merged accepting)
        minimized.States.Count.ShouldBe(2);

        // Both "a" and "b" should be accepted
        minimized.Execute("a").ShouldBeTrue();
        minimized.Execute("b").ShouldBeTrue();
        minimized.Execute("aa").ShouldBeTrue();
        minimized.Execute("bb").ShouldBeTrue();
        minimized.Execute("").ShouldBeFalse();
    }

    [Fact]
    public void MinimalizeDFA_DoesNotChangeAlreadyMinimalDFA()
    {
        // Arrange: Minimal DFA for (ab)*
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: true)
            .WithState(2, isStart: false, isAccepting: false)
            .WithTransition(1, 2, 'a')
            .WithTransition(2, 1, 'b')
            .Build();

        // Act
        var minimized = dfa.MinimalizeDFA();

        // Assert: Should have same number of states and same language
        minimized.States.Count.ShouldBe(2);
        minimized.Execute("").ShouldBeTrue();
        minimized.Execute("ab").ShouldBeTrue();
        minimized.Execute("abab").ShouldBeTrue();
        minimized.Execute("a").ShouldBeFalse();
        minimized.Execute("b").ShouldBeFalse();
        minimized.Execute("aba").ShouldBeFalse();
    }

    [Fact]
    public void MinimalizeDFA_RemovesUnreachableStates()
    {
        // Arrange: DFA with an unreachable state (state 4)
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithState(3, isStart: false, isAccepting: false)
            .WithState(4, isStart: false, isAccepting: true) // unreachable
            .WithTransition(1, 2, 'a')
            .WithTransition(2, 3, 'b')
            .WithTransition(3, 1, 'a')
            .Build();

        // Act
        var minimized = dfa.MinimalizeDFA();

        // Assert: State 4 should be removed
        minimized.States.Any(s => s.Id == 4).ShouldBeFalse();
        minimized.Execute("a").ShouldBeTrue();
        minimized.Execute("ab").ShouldBeFalse();
    }

    [Fact]
    public void MinimalizeDFA_MergesEquivalentNonAcceptingStates()
    {
        // Arrange
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: false)
            .WithState(3, isStart: false, isAccepting: false)
            .WithState(4, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'x')
            .WithTransition(2, 4, 'a')
            .WithTransition(2, 2, 'b')
            .WithTransition(3, 4, 'a')
            .WithTransition(3, 3, 'b')
            .Build();

        // Act
        var minimized = dfa.MinimalizeDFA();

        // Assert: states 2 and 3 are equivalent and should be merged
        minimized.States.Count.ShouldBeLessThan(dfa.States.Count);
        // language preserved
        minimized.Execute("xba").ShouldBeTrue();
        minimized.Execute("xb").ShouldBeFalse();
    }

    [Fact]
    public void MinimalizeDFA_AllStatesEquivalent_MergesToSingleState()
    {
        // Arrange: three accepting states with identical self-loop behavior
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: true)
            .WithState(2, isStart: false, isAccepting: true)
            .WithState(3, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .WithTransition(2, 2, 'a')
            .WithTransition(3, 2, 'a')
            .Build();

        // Act
        var minimized = dfa.MinimalizeDFA();

        // Assert: all accepting states with identical behavior should be merged
        minimized.States.Count.ShouldBe(1);
        minimized.Execute("").ShouldBeTrue();
        minimized.Execute("aaaa").ShouldBeTrue();
    }

    [Fact]
    public void MinimalizeDFA_PartialAlphabet_HandlesMissingTransitionsCorrectly()
    {
        // Arrange: states that differ only by a missing transition should not be merged
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithState(3, isStart: false, isAccepting: false)
            // 2 has an 'a' transition to itself; 3 lacks 'a' transition
            .WithTransition(1, 3, 'b')
            .WithTransition(1, 2, 'a')
            .WithTransition(2, 2, 'a')
            .Build();

        // Act
        var minimized = dfa.MinimalizeDFA();

        // Assert: state 2 and 3 are distinguishable due to missing 'a' transition
        minimized.States.Count.ShouldBeGreaterThan(1);
        minimized.Execute("a").ShouldBeTrue();
        minimized.Execute("b").ShouldBeFalse();
    }

    [Fact]
    public void MinimalizeDFA_PictureLikeStructure_MergesExpectedStates()
    {
        // Arrange: construct DFA similar to the provided sketch where two states behave identically
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: false)   // q1
            .WithState(2, isStart: false, isAccepting: true)   // q2 (accepting)
            .WithState(3, isStart: false, isAccepting: false)  // q3
            .WithState(4, isStart: false, isAccepting: false)  // q4
            .WithState(5, isStart: false, isAccepting: false)  // q5 
                                                               // transitions to mirror the sketch
            .WithTransition(1, 2, 'd')
            .WithTransition(2, 3, 'b')
            .WithTransition(2, 4, 'a')
            .WithTransition(3, 4, 'c')
            .WithTransition(4, 5, 'a')
            .WithTransition(4, 5, 'd')
            .WithTransition(5, 2, 'd')
            .Build();

        // Act
        var minimized = dfa.MinimalizeDFA();

        // Assert
        minimized.States.Count.ShouldBeLessThan(dfa.States.Count);
        // Basic language checks
        minimized.Execute("d").ShouldBeTrue();
        minimized.Execute("dbcddaad").ShouldBeTrue();
        minimized.Execute("ab").ShouldBeFalse();
        minimized.Execute("ad").ShouldBeFalse();
    }

    [Fact]
    public void MinimalizeDFA_PictureLikeStructure_StateMappingIsReported()
    {
        // Arrange: same picture-like DFA
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithState(3, isStart: false, isAccepting: false)
            .WithState(4, isStart: false, isAccepting: false)
            .WithState(5, isStart: false, isAccepting: false)
            .WithTransition(1, 2, 'd')
            .WithTransition(2, 3, 'b')
            .WithTransition(2, 4, 'a')
            .WithTransition(3, 4, 'c')
            .WithTransition(4, 5, 'a')
            .WithTransition(4, 5, 'd')
            .WithTransition(5, 2, 'd')
            .Build();

        // Act
        var minimized = dfa.MinimalizeDFA();

        // Assert: mapping groups (IDs for minimized states are implementation-dependent)
        minimized.StateMapping.ShouldNotBeNull();
        minimized.StateMapping.Keys.ShouldContain(1);
        minimized.StateMapping.Keys.ShouldContain(2);
        minimized.StateMapping.Keys.ShouldContain(3);
        minimized.StateMapping.Keys.ShouldContain(4);
        minimized.StateMapping.Keys.ShouldContain(5);

        // states 1 and 5 should map to the same new state id
        minimized.StateMapping[1].ShouldBe(minimized.StateMapping[5]);

        // state 2 should map to a different new state id (its own group)
        minimized.StateMapping[2].ShouldNotBe(minimized.StateMapping[1]);

        // verify merged groups content using the mapping
        minimized.MergedStateGroups.ShouldNotBeNull();
        var groupFor1 = minimized.MergedStateGroups[minimized.StateMapping[1]];
        groupFor1.SetEquals(new HashSet<int> { 1, 5 }).ShouldBeTrue();

        var groupFor2 = minimized.MergedStateGroups[minimized.StateMapping[2]];
        groupFor2.SetEquals(new HashSet<int> { 2 }).ShouldBeTrue();

        var groupFor3 = minimized.MergedStateGroups[minimized.StateMapping[3]];
        groupFor3.SetEquals(new HashSet<int> { 3 }).ShouldBeTrue();

        var groupFor4 = minimized.MergedStateGroups[minimized.StateMapping[4]];
        groupFor4.SetEquals(new HashSet<int> { 4 }).ShouldBeTrue();
    }

    [Fact]
    public void MinimalizeDFA_GetMinimizationReport_IncludesCorrectMappings()
    {
        // Arrange
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithState(3, isStart: false, isAccepting: false)
            .WithState(4, isStart: false, isAccepting: false)
            .WithState(5, isStart: false, isAccepting: false)
            .WithTransition(1, 2, 'd')
            .WithTransition(2, 3, 'b')
            .WithTransition(2, 4, 'a')
            .WithTransition(3, 4, 'c')
            .WithTransition(4, 5, 'a')
            .WithTransition(4, 5, 'd')
            .WithTransition(5, 2, 'd')
            .Build();

        // Act
        var minimized = dfa.MinimalizeDFA();
        var report = minimized.GetMinimizationReport();

        // Assert: report contains representation for each merged group
        foreach (var kv in minimized.MergedStateGroups)
        {
            var originals = string.Join(", ", kv.Value.OrderBy(id => id));
            var expected = $"{{{originals}}}";
            report.ShouldContain(expected);
        }
    }
}

public class DFABuilder
{
    private readonly DFA dfa = new();

    public DFABuilder WithState(int id, bool isStart = false, bool isAccepting = false)
    {
        dfa.AddState(new State { Id = id, IsStart = isStart, IsAccepting = isAccepting });
        return this;
    }

    public DFABuilder WithTransition(int fromStateId, int toStateId, char symbol)
    {
        dfa.AddTransition(fromStateId, toStateId, symbol);
        return this;
    }

    public DFA Build()
    {
        return dfa;
    }
}
