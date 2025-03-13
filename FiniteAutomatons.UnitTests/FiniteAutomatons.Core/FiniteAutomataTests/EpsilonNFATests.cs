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
        var states = new List<State>
        {
            new() { Id = 1, IsStart = true, IsAccepting = false },
            new() { Id = 2, IsStart = false, IsAccepting = true }
        };

        var transitions = new List<Transition>
        {
            new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
            new() { FromStateId = 1, ToStateId = 2, Symbol = '\0' }
        };

        var automata = new EpsilonNFA();
        automata.States.AddRange(states);
        automata.Transitions.AddRange(transitions);

        // Act
        var result = automata.Execute("a");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void Execute_InvalidInput_ShouldReturnFalse()
    {
        // Arrange
        var states = new List<State>
        {
            new() { Id = 1, IsStart = true, IsAccepting = false },
            new() { Id = 2, IsStart = false, IsAccepting = true }
        };

        var transitions = new List<Transition>
        {
            new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }
        };

        var automata = new EpsilonNFA();
        automata.States.AddRange(states);
        automata.Transitions.AddRange(transitions);

        // Act
        var result = automata.Execute("b");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Execute_NoStartState_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var states = new List<State>
        {
            new() { Id = 1, IsStart = false, IsAccepting = false },
            new() { Id = 2, IsStart = false, IsAccepting = true }
        };

        var transitions = new List<Transition>
        {
            new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }
        };

        var automata = new EpsilonNFA();
        automata.States.AddRange(states);
        automata.Transitions.AddRange(transitions);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => automata.Execute("a"));
    }

    [Fact]
    public void Execute_EmptyInput_ShouldReturnFalse()
    {
        // Arrange
        var states = new List<State>
        {
            new() { Id = 1, IsStart = true, IsAccepting = false },
            new() { Id = 2, IsStart = false, IsAccepting = true }
        };

        var transitions = new List<Transition>
        {
            new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }
        };

        var automata = new EpsilonNFA();
        automata.States.AddRange(states);
        automata.Transitions.AddRange(transitions);

        // Act
        var result = automata.Execute("");

        // Assert
        result.ShouldBeFalse();
    }
}
