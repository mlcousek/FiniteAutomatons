using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using Shouldly;
using State = FiniteAutomatons.Core.Models.DoMain.State;

namespace FiniteAutomatons.UnitTests.FiniteAutomatons.Core.FiniteAutomataTests;

public class EpsilonNFATests
{
    ////////// Execute method tests for EpsilonNFA
    ///
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

    ////////// Execute All tests for EpsilonNFA

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

    ////////// Step Forward tests for EpsilonNFA

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

    //////////// Start Execution tests for EpsilonNFA

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
