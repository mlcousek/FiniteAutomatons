using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using Shouldly;

namespace FiniteAutomatons.UnitTests.FiniteAutomatons.Core.FiniteAutomataTests;

public class DFATests
{
    ////////// Execute method tests for DFA

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
        var result = dfa.Execute("a"); // Ends in state 2, which is not accepting

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

    ////////// Execute All tests for DFA

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

    ////////// Step Forward tests for DFA

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

    //////////// Start Execution tests for DFA

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
        // For empty input, execution is immediately finished
        execState.IsFinished.ShouldBeTrue();
    }

    ////////// Step Backward tests for DFA

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

    // Additional tests 

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
