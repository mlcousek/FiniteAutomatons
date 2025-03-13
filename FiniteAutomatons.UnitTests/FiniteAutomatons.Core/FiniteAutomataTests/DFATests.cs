using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using Shouldly;

namespace FiniteAutomatons.UnitTests.FiniteAutomatons.Core.FiniteAutomataTests;

public class DFATests
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
