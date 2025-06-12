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
