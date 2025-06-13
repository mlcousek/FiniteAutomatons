using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using Shouldly;
using State = FiniteAutomatons.Core.Models.DoMain.State;

namespace FiniteAutomatons.UnitTests.FiniteAutomatons.Core.FiniteAutomataTests;

public class NFATests
{
    [Fact]
    public void Execute_ValidInput_ShouldReturnTrue()
    {
        // Arrange
        var nfa = new NFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .WithTransition(1, 2, 'b')
            .Build();

        // Act
        var result = nfa.Execute("a");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Execute_InvalidInput_ShouldReturnFalse()
    {
        // Arrange
        var nfa = new NFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        // Act
        var result = nfa.Execute("b");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Execute_NoStartState_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var nfa = new NFABuilder()
            .WithState(1, isStart: false, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => nfa.Execute("a"));
    }

    [Fact]
    public void Execute_EmptyInput_ShouldReturnFalse()
    {
        // Arrange
        var nfa = new NFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        // Act
        var result = nfa.Execute("");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Execute_MultiplePathsToAcceptingState_ShouldReturnTrue()
    {
        // Arrange
        var nfa = new NFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: false)
            .WithState(3, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .WithTransition(1, 3, 'a') // Multiple transitions with the same symbol
            .Build();

        // Act
        var result = nfa.Execute("a");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Execute_StartStateAlsoAccepting_EmptyInputShouldReturnTrue()
    {
        // Arrange
        var nfa = new NFABuilder()
            .WithState(1, isStart: true, isAccepting: true)
            .WithState(2, isStart: false, isAccepting: false)
            .WithTransition(1, 2, 'a')
            .Build();

        // Act
        var result = nfa.Execute("");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Execute_ComplexNonDeterministicPath_ShouldReturnTrue()
    {
        // Arrange
        var nfa = new NFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: false)
            .WithState(3, isStart: false, isAccepting: false)
            .WithState(4, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .WithTransition(1, 3, 'a')
            .WithTransition(2, 4, 'b')
            .WithTransition(3, 4, 'c')
            .Build();

        // Act & Assert
        nfa.Execute("ab").ShouldBeTrue();  // Path 1->2->4
        nfa.Execute("ac").ShouldBeTrue();  // Path 1->3->4
        nfa.Execute("ad").ShouldBeFalse(); // No valid path
    }

    [Fact]
    public void Execute_NonDeterministicChoices_FollowsAllPaths()
    {
        // Arrange
        var nfa = new NFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: false)
            .WithState(3, isStart: false, isAccepting: false)
            .WithState(4, isStart: false, isAccepting: true)
            .WithState(5, isStart: false, isAccepting: false)
            .WithTransition(1, 2, 'a')
            .WithTransition(1, 3, 'a')
            .WithTransition(2, 4, 'b')
            .WithTransition(3, 5, 'b')
            .Build();

        // Act & Assert
        nfa.Execute("ab").ShouldBeTrue(); // At least one path leads to an accepting state
    }

    [Theory]
    [InlineData("a", true)]
    [InlineData("ab", false)]
    [InlineData("b", false)]
    [InlineData("", false)]
    public void Execute_VariousInputs_ShouldReturnExpectedResults(string input, bool expected)
    {
        // Arrange
        var nfa = new NFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        // Act
        var result = nfa.Execute(input);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void Execute_MultipleSymbolsWithSameTransition_ShouldWorkCorrectly()
    {
        // Arrange
        var nfa = new NFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: false)
            .WithState(3, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .WithTransition(2, 2, 'a') // Self-loop
            .WithTransition(2, 3, 'b')
            .Build();

        // Act & Assert
        nfa.Execute("ab").ShouldBeTrue();
        nfa.Execute("aab").ShouldBeTrue();
        nfa.Execute("aaaaaab").ShouldBeTrue();
        nfa.Execute("b").ShouldBeFalse();
    }

    ////////// Execute All tests for NFA

    [Fact]
    public void ExecuteAll_ProcessesEntireInputAndSetsIsAccepted()
    {
        var nfa = new NFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        var state = nfa.StartExecution("a");
        nfa.ExecuteAll(state);

        state.CurrentStates.ShouldNotBeNull();
        state.CurrentStates.ShouldContain(2);
        state.Position.ShouldBe(1);
        state.IsAccepted.ShouldBe(true);
    }

    [Fact]
    public void ExecuteAll_EmptyInput_SetsIsAcceptedBasedOnStartState()
    {
        var nfa = new NFABuilder()
            .WithState(1, isStart: true, isAccepting: true)
            .Build();

        var state = nfa.StartExecution("");
        nfa.ExecuteAll(state);

        state.CurrentStates.ShouldNotBeNull();
        state.CurrentStates.ShouldContain(1);
        state.Position.ShouldBe(0);
        state.IsAccepted.ShouldBe(true);
    }

    ////////// Step Forward tests for NFA

    [Fact]
    public void StepForward_ValidTransition_UpdatesStatesAndPosition()
    {
        var nfa = new NFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        var state = nfa.StartExecution("a");
        nfa.StepForward(state);

        state.CurrentStates.ShouldNotBeNull();
        state.CurrentStates.ShouldContain(2);
        state.Position.ShouldBe(1);
        state.IsAccepted.ShouldBe(true);
    }

    [Fact]
    public void StepForward_NoValidTransition_SetsIsAcceptedFalseAndFinishes()
    {
        var nfa = new NFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        var state = nfa.StartExecution("b");
        nfa.StepForward(state);

        state.IsAccepted.ShouldBe(false);
        state.Position.ShouldBe(1);
    }
    [Fact]
    public void StepForward_AtEndOfInput_SetsIsAcceptedBasedOnCurrentStates()
    {
        var nfa = new NFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        var state = nfa.StartExecution("a");
        nfa.StepForward(state); // Move to state 2, position 1
        nfa.StepForward(state); // Should check acceptance

        state.IsAccepted.ShouldBe(true);
    }

    //////////// Start Execution tests for NFA

    [Fact]
    public void StartExecution_WithValidStartState_ShouldInitializeStateCorrectly()
    {
        var nfa = new NFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        var execState = nfa.StartExecution("a");

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
        var nfa = new NFABuilder()
            .WithState(1, isStart: false, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .Build();

        Should.Throw<InvalidOperationException>(() => nfa.StartExecution("a"));
    }

    [Fact]
    public void StartExecution_WithEmptyInput_ShouldInitializeStateCorrectly()
    {
        var nfa = new NFABuilder()
            .WithState(1, isStart: true, isAccepting: true)
            .Build();

        var execState = nfa.StartExecution("");

        execState.CurrentStates.ShouldNotBeNull();
        execState.CurrentStates.ShouldContain(1);
        execState.Input.ShouldBe("");
        execState.Position.ShouldBe(0);
        execState.IsAccepted.ShouldBeNull();
        execState.IsFinished.ShouldBeTrue();
    }

    ////////// Step Backward tests for NFA

    [Fact]
    public void StepBackward_AfterStepForward_RestoresPreviousStateAndPosition()
    {
        // Arrange
        var nfa = new NFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        var state = nfa.StartExecution("a");
        nfa.StepForward(state); // Move to state 2, position 1

        // Act
        nfa.StepBackward(state); // Should move back to position 0 and restore initial state

        // Assert
        state.Position.ShouldBe(0);
        state.CurrentStates.ShouldNotBeNull();
        state.CurrentStates.ShouldContain(1);
        state.IsAccepted.ShouldBeNull();
    }

    [Fact]
    public void StepBackward_AtStartPosition_DoesNothing()
    {
        // Arrange
        var nfa = new NFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        var state = nfa.StartExecution("a");

        // Act
        nfa.StepBackward(state); // Already at position 0

        // Assert
        state.Position.ShouldBe(0);
        state.CurrentStates.ShouldNotBeNull();
        state.CurrentStates.ShouldContain(1);
        state.IsAccepted.ShouldBeNull();
    }

    [Fact]
    public void StepBackward_MultipleSteps_RestoresStatesCorrectly()
    {
        // Arrange
        var nfa = new NFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: false)
            .WithState(3, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .WithTransition(2, 3, 'b')
            .Build();

        var state = nfa.StartExecution("ab");
        nfa.StepForward(state); // 'a', to state 2, pos 1
        nfa.StepForward(state); // 'b', to state 3, pos 2

        // Act
        nfa.StepBackward(state); // back to pos 1, should be at state 2
        state.Position.ShouldBe(1);
        state.CurrentStates.ShouldNotBeNull();
        state.CurrentStates.ShouldContain(2);

        nfa.StepBackward(state); // back to pos 0, should be at state 1
        state.Position.ShouldBe(0);
        state.CurrentStates.ShouldNotBeNull();
        state.CurrentStates.ShouldContain(1);
    }

    // Aditional test for Epsilon NFA 
    [Fact]
    public void StepForward_PushesCurrentStatesToHistory_NFA()
    {
        // Arrange
        var nfa = new NFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        var state = nfa.StartExecution("a");

        // Act
        nfa.StepForward(state);

        // Assert
        state.StateHistory.Count.ShouldBe(1);
        state.StateHistory.Peek().ShouldContain(1); // The start state was pushed
    }

    [Fact]
    public void StepBackward_PopsFromHistory_AndRestoresPreviousStates_NFA()
    {
        // Arrange
        var nfa = new NFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        var state = nfa.StartExecution("a");
        nfa.StepForward(state); // Move to state 2, pos 1

        // Act
        nfa.StepBackward(state); // Should restore to state 1

        // Assert
        state.CurrentStates.ShouldNotBeNull();
        state.CurrentStates.ShouldContain(1);
        state.StateHistory.Count.ShouldBe(0);
    }

    [Fact]
    public void StepForward_And_StepBackward_MultipleSteps_ManageHistoryCorrectly_NFA()
    {
        // Arrange
        var nfa = new NFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: false)
            .WithState(3, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .WithTransition(2, 3, 'b')
            .Build();

        var state = nfa.StartExecution("ab");
        nfa.StepForward(state); // 'a' -> 2
        nfa.StepForward(state); // 'b' -> 3

        // Assert history after two steps
        state.StateHistory.Count.ShouldBe(2);
        state.StateHistory.ToArray()[1].ShouldContain(1); // First pushed state
        state.StateHistory.ToArray()[0].ShouldContain(2); // Second pushed state

        // Act: Step backward twice
        nfa.StepBackward(state); // Should restore to state 2
        state.CurrentStates.ShouldNotBeNull();
        state.CurrentStates.ShouldContain(2);
        state.StateHistory.Count.ShouldBe(1);

        nfa.StepBackward(state); // Should restore to state 1
        state.CurrentStates.ShouldContain(1);
        state.StateHistory.Count.ShouldBe(0);
    }
}

public class NFABuilder
{
    private readonly NFA nfa = new();

    public NFABuilder WithState(int id, bool isStart = false, bool isAccepting = false)
    {
        nfa.AddState(new State { Id = id, IsStart = isStart, IsAccepting = isAccepting });
        return this;
    }

    public NFABuilder WithTransition(int fromStateId, int toStateId, char symbol)
    {
        nfa.AddTransition(fromStateId, toStateId, symbol);
        return this;
    }

    public NFA Build()
    {
        return nfa;
    }
}
