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
