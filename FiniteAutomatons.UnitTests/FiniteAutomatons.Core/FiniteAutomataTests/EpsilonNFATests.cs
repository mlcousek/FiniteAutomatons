using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using Shouldly;
using State = FiniteAutomatons.Core.Models.DoMain.State;

namespace FiniteAutomatons.UnitTests.FiniteAutomatons.Core.FiniteAutomataTests;

public class EpsilonNFATests
{
    [Fact]
    public void Execute_ValidInput_ShouldReturnTrue()
    {
        // Arrange
        var enfa = new EpsilonNFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .WithEpsilonTransition(1, 2)
            .Build();

        // Act
        var result = enfa.Execute("a");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Execute_InvalidInput_ShouldReturnFalse()
    {
        // Arrange
        var enfa = new EpsilonNFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        // Act
        var result = enfa.Execute("b");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Execute_NoStartState_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var enfa = new EpsilonNFABuilder()
            .WithState(1, isStart: false, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => enfa.Execute("a"));
    }

    [Fact]
    public void Execute_EmptyInput_ShouldReturnFalse()
    {
        // Arrange
        var enfa = new EpsilonNFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        // Act
        var result = enfa.Execute("");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Execute_EpsilonTransitionToAcceptingState_ShouldReturnTrue()
    {
        // Arrange
        var enfa = new EpsilonNFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithEpsilonTransition(1, 2)
            .Build();

        // Act
        var result = enfa.Execute("");  // No input should still reach accepting state via epsilon

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Execute_ChainedEpsilonTransitions_ShouldReachAcceptingState()
    {
        // Arrange
        var enfa = new EpsilonNFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: false)
            .WithState(3, isStart: false, isAccepting: true)
            .WithEpsilonTransition(1, 2)
            .WithEpsilonTransition(2, 3)
            .Build();

        // Act
        var result = enfa.Execute("");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Execute_EpsilonTransitionAfterSymbol_ShouldReachAcceptingState()
    {
        // Arrange
        var enfa = new EpsilonNFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: false)
            .WithState(3, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .WithEpsilonTransition(2, 3)
            .Build();

        // Act
        var result = enfa.Execute("a");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Execute_ComplexEpsilonPaths_ShouldFollowAllPaths()
    {
        // Arrange
        var enfa = new EpsilonNFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: false)
            .WithState(3, isStart: false, isAccepting: false)
            .WithState(4, isStart: false, isAccepting: true)
            .WithState(5, isStart: false, isAccepting: false)
            .WithTransition(1, 2, 'a')
            .WithEpsilonTransition(1, 3)    // Epsilon from start to state 3
            .WithEpsilonTransition(2, 5)    // Epsilon from state 2 to state 5
            .WithTransition(3, 4, 'b')      // Path through state 3
            .WithTransition(2, 5, 'b')      // Path through state 2
            .WithEpsilonTransition(5, 4)    // Epsilon from 5 to accepting state
            .Build();

        // Act & Assert
        enfa.Execute("ab").ShouldBeTrue();  // 1->2->5->4
        enfa.Execute("b").ShouldBeTrue();   // 1->3->4 via epsilon
        enfa.Execute("a").ShouldBeTrue();   // 1->2->5->4 via epsilon
    }

    [Fact]
    public void Execute_EpsilonLoops_ShouldNotCauseInfiniteLoop()
    {
        // Arrange
        var enfa = new EpsilonNFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithEpsilonTransition(1, 2)
            .WithEpsilonTransition(2, 1)  // Loop back to 1
            .Build();

        // Act
        var result = enfa.Execute("");

        // Assert
        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData("", true)]     // Via epsilon
    [InlineData("a", true)]    // Via input + epsilon
    [InlineData("b", false)]   // No path
    [InlineData("aa", false)]  // Too long
    public void Execute_VariousInputs_ShouldReturnExpectedResults(string input, bool expected)
    {
        // Arrange
        var enfa = new EpsilonNFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: false)
            .WithState(3, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .WithEpsilonTransition(2, 3)
            .WithEpsilonTransition(1, 3)
            .Build();

        // Act
        var result = enfa.Execute(input);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void Execute_MultipleEpsilonPathsToAcceptingStates_ShouldReturnTrue()
    {
        // Arrange
        var enfa = new EpsilonNFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: false)
            .WithState(3, isStart: false, isAccepting: true)
            .WithState(4, isStart: false, isAccepting: true)
            .WithEpsilonTransition(1, 2)
            .WithEpsilonTransition(1, 3)
            .WithEpsilonTransition(2, 4)
            .Build();

        // Act
        var result = enfa.Execute("");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Execute_StartStateAlsoAccepting_EmptyInputShouldReturnTrue()
    {
        // Arrange
        var enfa = new EpsilonNFABuilder()
            .WithState(1, isStart: true, isAccepting: true)
            .WithState(2, isStart: false, isAccepting: false)
            .WithTransition(1, 2, 'a')
            .Build();

        // Act
        var result = enfa.Execute("");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ExecuteAll_ProcessesEntireInputAndSetsIsAccepted()
    {
        var enfa = new EpsilonNFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        var state = enfa.StartExecution("a");
        enfa.ExecuteAll(state);

        state.CurrentStates.ShouldNotBeNull();
        state.CurrentStates.ShouldContain(2);
        state.Position.ShouldBe(1);
        state.IsAccepted.ShouldBe(true);
    }

    [Fact]
    public void ExecuteAll_EmptyInput_SetsIsAcceptedBasedOnStartState()
    {
        var enfa = new EpsilonNFABuilder()
            .WithState(1, isStart: true, isAccepting: true)
            .Build();

        var state = enfa.StartExecution("");
        enfa.ExecuteAll(state);

        state.CurrentStates.ShouldNotBeNull();
        state.CurrentStates.ShouldContain(1);
        state.Position.ShouldBe(0);
        state.IsAccepted.ShouldBe(true);
    }

    [Fact]
    public void StepForward_ValidTransition_UpdatesStatesAndPosition()
    {
        var enfa = new EpsilonNFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        var state = enfa.StartExecution("a");
        enfa.StepForward(state);

        state.CurrentStates.ShouldNotBeNull();
        state.CurrentStates.ShouldContain(2);
        state.Position.ShouldBe(1);
        state.IsAccepted.ShouldBe(true);
    }

    [Fact]
    public void StepForward_NoValidTransition_SetsIsAcceptedFalseAndFinishes()
    {
        var enfa = new EpsilonNFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        var state = enfa.StartExecution("b");
        enfa.StepForward(state);

        state.IsAccepted.ShouldBe(false);
        state.Position.ShouldBe(1);
    }

    [Fact]
    public void StepForward_AtEndOfInput_SetsIsAcceptedBasedOnCurrentStates()
    {
        var enfa = new EpsilonNFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        var state = enfa.StartExecution("a");
        enfa.StepForward(state); // Move to state 2, position 1
        enfa.StepForward(state); // Should check acceptance

        state.IsAccepted.ShouldBe(true);
    }

    [Fact]
    public void StepBackward_AfterStepForward_RestoresPreviousStateAndPosition_WithEpsilon()
    {
        // Arrange
        var enfa = new EpsilonNFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithEpsilonTransition(1, 2)
            .WithTransition(1, 2, 'a')
            .Build();

        var state = enfa.StartExecution("a");
        enfa.StepForward(state); // Move to state 2, position 1

        // Act
        enfa.StepBackward(state); // Should move back to position 0 and restore epsilon closure of initial state

        // Assert
        state.Position.ShouldBe(0);
        state.CurrentStates.ShouldNotBeNull();
        state.CurrentStates.ShouldContain(1);
        state.CurrentStates.ShouldContain(2); // Epsilon closure includes both 1 and 2
        state.IsAccepted.ShouldBeNull();
    }

    [Fact]
    public void StepBackward_AtStartPosition_DoesNothing_WithEpsilon()
    {
        // Arrange
        var enfa = new EpsilonNFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithEpsilonTransition(1, 2)
            .Build();

        var state = enfa.StartExecution("a");

        // Act
        enfa.StepBackward(state); // Already at position 0

        // Assert
        state.Position.ShouldBe(0);
        state.CurrentStates.ShouldNotBeNull();
        state.CurrentStates.ShouldContain(1);
        state.CurrentStates.ShouldContain(2); // Epsilon closure includes both 1 and 2
        state.IsAccepted.ShouldBeNull();
    }

    [Fact]
    public void StepForward_NoValidTransition_SetsIsAcceptedFalseAndFinishes_EpsilonNFA()
    {
        var enfa = new EpsilonNFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        var state = enfa.StartExecution("ab");
        enfa.StepForward(state); // 'a' -> 2
        enfa.StepForward(state); // 'b' -> no valid transition

        state.CurrentStates.ShouldBeEmpty();
        state.IsAccepted.ShouldBe(false);
        state.Position.ShouldBe(2); // At end of input
    }

    [Fact]
    public void StepBackward_MultipleSteps_RestoresStatesCorrectly_WithEpsilon()
    {
        // Arrange
        var enfa = new EpsilonNFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: false)
            .WithState(3, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .WithEpsilonTransition(2, 3)
            .Build();

        var state = enfa.StartExecution("ab");
        enfa.StepForward(state); // 'a', to state 2 (and 3 via epsilon), pos 1
        enfa.StepForward(state); // 'b', no valid transition, pos 2

        // Act
        enfa.StepBackward(state); // back to pos 1, should be at state 2 and 3 (epsilon closure)
        state.Position.ShouldBe(1);
        state.CurrentStates.ShouldNotBeNull();
        state.CurrentStates.ShouldContain(2);
        state.CurrentStates.ShouldContain(3);

        enfa.StepBackward(state); // back to pos 0, should be at state 1 (and 3 via epsilon from 2)
        state.Position.ShouldBe(0);
        state.CurrentStates.ShouldNotBeNull();
        state.CurrentStates.ShouldContain(1);
        state.CurrentStates.Count.ShouldBe(1);
    }

    [Fact]
    public void StartExecution_WithValidStartState_ShouldInitializeStateCorrectly()
    {
        var enfa = new EpsilonNFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        var execState = enfa.StartExecution("a");

        execState.CurrentStates.ShouldNotBeNull();
        execState.CurrentStates.ShouldContain(1);
        execState.Input.ShouldBe("a");
        execState.Position.ShouldBe(0);
        execState.IsAccepted.ShouldBeNull();
        execState.IsFinished.ShouldBeFalse();
    }

    [Fact]
    public void StartExecution_NoStartState_ShouldThrowInvalidOperationException()
    {
        var enfa = new EpsilonNFABuilder()
            .WithState(1, isStart: false, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .Build();

        Should.Throw<InvalidOperationException>(() => enfa.StartExecution("a"));
    }

    [Fact]
    public void StartExecution_WithEmptyInput_ShouldInitializeStateCorrectly()
    {
        var enfa = new EpsilonNFABuilder()
            .WithState(1, isStart: true, isAccepting: true)
            .Build();

        var execState = enfa.StartExecution("");

        execState.CurrentStates.ShouldNotBeNull();
        execState.CurrentStates.ShouldContain(1);
        execState.Input.ShouldBe("");
        execState.Position.ShouldBe(0);
        execState.IsAccepted.ShouldBeNull();
        execState.IsFinished.ShouldBeTrue();
    }

    [Fact]
    public void StepForward_PushesCurrentStatesToHistory_EpsilonNFA()
    {
        // Arrange
        var enfa = new EpsilonNFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: false)
            .WithState(3, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .WithEpsilonTransition(2, 3)
            .Build();

        var state = enfa.StartExecution("a");

        // Act
        enfa.StepForward(state);

        // Assert
        state.StateHistory.Count.ShouldBe(1);
        state.StateHistory.Peek().ShouldContain(1); // The start state was pushed
    }

    [Fact]
    public void StepBackward_PopsFromHistory_AndRestoresPreviousStates_EpsilonNFA()
    {
        // Arrange
        var enfa = new EpsilonNFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: false)
            .WithState(3, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .WithEpsilonTransition(2, 3)
            .Build();

        var state = enfa.StartExecution("a");
        enfa.StepForward(state); // 'a' -> 2, epsilon to 3

        // Act
        enfa.StepBackward(state); // Should restore to state 1

        // Assert
        state.CurrentStates.ShouldNotBeNull();
        state.CurrentStates.ShouldContain(1);
        state.CurrentStates.Count.ShouldBe(1);
        state.StateHistory.Count.ShouldBe(0);
    }

    [Fact]
    public void StepForward_And_StepBackward_MultipleSteps_ManageHistoryCorrectly_EpsilonNFA()
    {
        // Arrange
        var enfa = new EpsilonNFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: false)
            .WithState(3, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .WithEpsilonTransition(2, 3)
            .Build();

        var state = enfa.StartExecution("ab");
        enfa.StepForward(state); // 'a' -> 2, epsilon to 3
        enfa.StepForward(state); // 'b' -> no valid transition

        // Assert history after two steps
        state.StateHistory.Count.ShouldBe(2);
        state.StateHistory.ToArray()[1].ShouldContain(1); // First pushed state
        state.StateHistory.ToArray()[0].ShouldContain(2);
        state.StateHistory.ToArray()[0].ShouldContain(3);

        // Act: Step backward twice
        enfa.StepBackward(state); // Should restore to state 2 and 3
        state.CurrentStates.ShouldNotBeNull();
        state.CurrentStates.ShouldContain(2);
        state.CurrentStates.ShouldContain(3);
        state.StateHistory.Count.ShouldBe(1);

        enfa.StepBackward(state); // Should restore to state 1
        state.CurrentStates.ShouldNotBeNull();
        state.CurrentStates.ShouldContain(1);
        state.CurrentStates.Count.ShouldBe(1);
        state.StateHistory.Count.ShouldBe(0);
    }

    [Fact]
    public void BackToStart_ResetsStateToInitialConditions_EpsilonNFA()
    {
        // Arrange
        var enfa = new EpsilonNFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithEpsilonTransition(1, 2)
            .Build();

        var state = enfa.StartExecution("a");
        enfa.StepForward(state); // Move forward, change state
        state.IsAccepted = true; // Simulate acceptance
        state.StateHistory.Push([1, 2]); // Simulate history

        // Act
        enfa.BackToStart(state);

        // Assert
        state.Position.ShouldBe(0);
        state.CurrentStates.ShouldNotBeNull();
        state.CurrentStates.ShouldContain(1);
        state.CurrentStates.ShouldContain(2); // Epsilon closure includes both 1 and 2
        state.CurrentStateId.ShouldBeNull();
        state.IsAccepted.ShouldBeNull();
        state.StateHistory.Count.ShouldBe(0);
    }

    [Fact]
    public void EpsilonNFA_FullWorkflow_ComplexScenario()
    {
        // Arrange: EpsilonNFA for language accepting "ab", "aac", "b", and "" (via epsilon)
        var enfa = new EpsilonNFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: false)
            .WithState(3, isStart: false, isAccepting: true)
            .WithState(4, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .WithTransition(2, 2, 'a') // self-loop on 'a'
            .WithTransition(2, 3, 'c')
            .WithTransition(1, 3, 'b')
            .WithEpsilonTransition(1, 4) // epsilon from start to accepting
            .Build();

        // Start execution with input "aac"
        var state = enfa.StartExecution("aac");

        // Step 1: 'a' -> state 2
        enfa.StepForward(state);
        state.CurrentStates.ShouldNotBeNull();
        state.CurrentStates.ShouldContain(2);
        state.Position.ShouldBe(1);
        state.IsAccepted.ShouldBeNull();

        // Step 2: 'a' -> state 2 (self-loop)
        enfa.StepForward(state);
        state.CurrentStates.ShouldNotBeNull();
        state.CurrentStates.ShouldContain(2);
        state.Position.ShouldBe(2);
        state.IsAccepted.ShouldBeNull();

        // Step 3: 'c' -> state 3 (accepting)
        enfa.StepForward(state);
        state.CurrentStates.ShouldNotBeNull();
        state.CurrentStates.ShouldContain(3);
        state.Position.ShouldBe(3);
        state.IsAccepted.ShouldBe(true);

        // Step backward: back to state 2
        enfa.StepBackward(state);
        state.CurrentStates.ShouldNotBeNull();
        state.CurrentStates.ShouldContain(2);
        state.Position.ShouldBe(2);
        state.IsAccepted.ShouldBeNull();

        // Step forward again: 'c' -> state 3
        enfa.StepForward(state);
        state.CurrentStates.ShouldNotBeNull();
        state.CurrentStates.ShouldContain(3);
        state.Position.ShouldBe(3);
        state.IsAccepted.ShouldBe(true);

        // ExecuteAll from current position (should remain at state 3, accepting)
        enfa.ExecuteAll(state);
        state.CurrentStates.ShouldNotBeNull();
        state.CurrentStates.ShouldContain(3);
        state.Position.ShouldBe(3);
        state.IsAccepted.ShouldBe(true);

        // Back to start
        enfa.BackToStart(state);
        state.Position.ShouldBe(0);
        state.CurrentStates.ShouldNotBeNull();
        state.CurrentStates.ShouldContain(1);
        state.CurrentStates.ShouldContain(4); // Epsilon closure includes both 1 and 4
        state.CurrentStateId.ShouldBeNull();
        state.IsAccepted.ShouldBeNull();
        state.StateHistory.Count.ShouldBe(0);

        // ExecuteAll from start (should finish at state 3, accepting, for input "aac")
        enfa.ExecuteAll(state);
        state.CurrentStates.ShouldNotBeNull();
        state.CurrentStates.ShouldContain(3);
        state.Position.ShouldBe(3);
        state.IsAccepted.ShouldBe(true);

        // Test epsilon-only acceptance (empty input)
        var emptyState = enfa.StartExecution("");
        enfa.ExecuteAll(emptyState);
        emptyState.CurrentStates.ShouldNotBeNull();
        emptyState.CurrentStates.ShouldContain(1);
        emptyState.CurrentStates.ShouldContain(4); // Epsilon closure includes accepting state
        emptyState.Position.ShouldBe(0);
        emptyState.IsAccepted.ShouldBe(true);

        // Test direct 'b' transition
        var bState = enfa.StartExecution("b");
        enfa.ExecuteAll(bState);
        bState.CurrentStates.ShouldNotBeNull();
        bState.CurrentStates.ShouldContain(3);
        bState.Position.ShouldBe(1);
        bState.IsAccepted.ShouldBe(true);
    }

    [Fact]
    public void ToNFA_RemovesEpsilonTransitionsAndPreservesLanguage()
    {
        // Arrange: EpsilonNFA with epsilon transition and normal transition
        var enfa = new EpsilonNFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .WithEpsilonTransition(1, 2)
            .Build();

        // Act
        var nfa = enfa.ToNFA();

        // Assert: NFA should have no epsilon transitions
        nfa.Transitions.ShouldAllBe(t => t.Symbol != '\0');

        // Accepting states should be preserved via epsilon closure
        nfa.States.First(s => s.Id == 1).IsAccepting.ShouldBeTrue();
        nfa.States.First(s => s.Id == 2).IsAccepting.ShouldBeTrue();

        // NFA should accept "" and "a"
        nfa.Execute("").ShouldBeTrue();
        nfa.Execute("a").ShouldBeTrue();
        nfa.Execute("b").ShouldBeFalse();
    }

    [Fact]
    public void ToNFA_ChainedEpsilonTransitions_ReachAcceptingState()
    {
        // Arrange: EpsilonNFA with chained epsilon transitions
        var enfa = new EpsilonNFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: false)
            .WithState(3, isStart: false, isAccepting: true)
            .WithEpsilonTransition(1, 2)
            .WithEpsilonTransition(2, 3)
            .Build();

        // Act
        var nfa = enfa.ToNFA();

        // Assert: All states in the epsilon closure of 1 should be accepting if any are accepting
        nfa.States.First(s => s.Id == 1).IsAccepting.ShouldBeTrue();
        nfa.States.First(s => s.Id == 2).IsAccepting.ShouldBeTrue();
        nfa.States.First(s => s.Id == 3).IsAccepting.ShouldBeTrue();

        // NFA should accept ""
        nfa.Execute("").ShouldBeTrue();
    }

    [Fact]
    public void ToNFA_TransitionsAreCorrectlyConverted()
    {
        // Arrange: EpsilonNFA with both epsilon and non-epsilon transitions
        var enfa = new EpsilonNFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: false)
            .WithState(3, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .WithEpsilonTransition(2, 3)
            .Build();

        // Act
        var nfa = enfa.ToNFA();

        // Assert: There should be a transition from 1 to 3 on 'a' (via 2's epsilon to 3)
        nfa.Transitions.ShouldContain(t => t.FromStateId == 1 && t.ToStateId == 3 && t.Symbol == 'a');
        // There should also be a transition from 1 to 2 on 'a'
        nfa.Transitions.ShouldContain(t => t.FromStateId == 1 && t.ToStateId == 2 && t.Symbol == 'a');
        // No epsilon transitions
        nfa.Transitions.ShouldAllBe(t => t.Symbol != '\0');
    }

    [Fact]
    public void ToDFA_FromEpsilonNFA_ProducesCorrectDFA()
    {
        // Arrange: eNFA that accepts "", "a", and "b" via epsilon transitions
        var enfa = new EpsilonNFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithEpsilonTransition(1, 2)
            .WithTransition(1, 2, 'a')
            .WithTransition(2, 2, 'b')
            .Build();

        // Act
        var nfa = enfa.ToNFA();
        var dfa = nfa.ToDFA();

        // Assert: DFA should accept "", "a", "b", "ab", "bb", "aab", etc.
        dfa.Execute("").ShouldBeTrue();
        dfa.Execute("a").ShouldBeTrue();
        dfa.Execute("b").ShouldBeTrue();
        dfa.Execute("ab").ShouldBeTrue();
        dfa.Execute("bb").ShouldBeTrue();
        dfa.Execute("abb").ShouldBeTrue();
        dfa.Execute("aab").ShouldBeFalse();
        dfa.Execute("ba").ShouldBeFalse();
    }
}

public class EpsilonNFABuilder
{
    private readonly EpsilonNFA enfa = new();

    public EpsilonNFABuilder WithState(int id, bool isStart = false, bool isAccepting = false)
    {
        enfa.AddState(new State { Id = id, IsStart = isStart, IsAccepting = isAccepting });
        return this;
    }

    public EpsilonNFABuilder WithTransition(int fromStateId, int toStateId, char symbol)
    {
        enfa.AddTransition(fromStateId, toStateId, symbol);
        return this;
    }

    public EpsilonNFABuilder WithEpsilonTransition(int fromStateId, int toStateId)
    {
        enfa.AddEpsilonTransition(fromStateId, toStateId);
        return this;
    }

    public EpsilonNFA Build()
    {
        return enfa;
    }
}
