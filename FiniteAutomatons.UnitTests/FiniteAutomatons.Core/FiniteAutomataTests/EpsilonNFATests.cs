using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using Shouldly;

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
