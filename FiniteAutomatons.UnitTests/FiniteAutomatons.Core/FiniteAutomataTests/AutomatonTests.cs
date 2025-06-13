using FiniteAutomatons.Core.Models.DoMain;
using Shouldly;

namespace FiniteAutomatons.UnitTests.FiniteAutomatons.Core.FiniteAutomataTests;

public class AutomatonTests
{
    [Fact]
    public void AddState_ValidState_ShouldAddState()
    {
        // Arrange
        var state = new State { Id = 1, IsStart = true };
        var automaton = new TestAutomatonBuilder()
            .WithState(state)
            .Build();

        // Act
        var result = automaton.States;

        // Assert
        result.ShouldContain(state);
    }

    [Fact]
    public void AddState_MultipleStartStates_ShouldThrowException()
    {
        // Arrange
        var state1 = new State { Id = 1, IsStart = true };
        var state2 = new State { Id = 2, IsStart = true };
        var builder = new TestAutomatonBuilder()
            .WithState(state1);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => builder.WithState(state2));
    }

#nullable disable
    [Fact]
    public void AddState_NullState_ShouldThrowArgumentNullException()
    {
        // Arrange
        var automaton = new TestAutomatonBuilder().Build();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => automaton.AddState(null));
    }
#nullable enable

    [Fact]
    public void SetStartState_ValidStateId_ShouldSetStartState()
    {
        // Arrange
        var state1 = new State { Id = 1, IsStart = true };
        var state2 = new State { Id = 2, IsStart = false };
        var automaton = new TestAutomatonBuilder()
            .WithState(state1)
            .WithState(state2)
            .Build();

        // Act
        automaton.SetStartState(2);

        // Assert
        state1.IsStart.ShouldBeFalse();
        state2.IsStart.ShouldBeTrue();
    }

    [Fact]
    public void SetStartState_InvalidStateId_ShouldThrowArgumentException()
    {
        // Arrange
        var state1 = new State { Id = 1, IsStart = true };
        var automaton = new TestAutomatonBuilder()
            .WithState(state1)
            .Build();

        // Act & Assert
        Should.Throw<ArgumentException>(() => automaton.SetStartState(2));
    }

    [Fact]
    public void AddTransition_ValidTransition_ShouldAddTransition()
    {
        // Arrange
        var state1 = new State { Id = 1, IsStart = true };
        var state2 = new State { Id = 2, IsStart = false };
        var transition = new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a' };
        var automaton = new TestAutomatonBuilder()
            .WithState(state1)
            .WithState(state2)
            .WithTransition(transition)
            .Build();

        // Act
        var result = automaton.Transitions;

        // Assert
        result.ShouldContain(transition);
    }

#nullable disable
    [Fact]
    public void AddTransition_NullTransition_ShouldThrowArgumentNullException()
    {
        // Arrange
        var automaton = new TestAutomatonBuilder().Build();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => automaton.AddTransition(null));
    }
#nullable enable

    [Fact]
    public void AddTransition_InvalidFromStateId_ShouldThrowArgumentException()
    {
        // Arrange
        var state1 = new State { Id = 1, IsStart = true };
        var state2 = new State { Id = 2, IsStart = false };
        var automaton = new TestAutomatonBuilder()
            .WithState(state1)
            .WithState(state2)
            .Build();

        var transition = new Transition { FromStateId = 3, ToStateId = 2, Symbol = 'a' };

        // Act & Assert
        Should.Throw<ArgumentException>(() => automaton.AddTransition(transition));
    }

    [Fact]
    public void AddTransition_InvalidToStateId_ShouldThrowArgumentException()
    {
        // Arrange
        var state1 = new State { Id = 1, IsStart = true };
        var state2 = new State { Id = 2, IsStart = false };
        var automaton = new TestAutomatonBuilder()
            .WithState(state1)
            .WithState(state2)
            .Build();

        var transition = new Transition { FromStateId = 1, ToStateId = 3, Symbol = 'a' };

        // Act & Assert
        Should.Throw<ArgumentException>(() => automaton.AddTransition(transition));
    }

    [Fact]
    public void FindTransitionsFromState_ValidStateId_ShouldReturnTransitions()
    {
        // Arrange
        var state1 = new State { Id = 1, IsStart = true };
        var state2 = new State { Id = 2, IsStart = false };
        var transition = new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a' };
        var automaton = new TestAutomatonBuilder()
            .WithState(state1)
            .WithState(state2)
            .WithTransition(transition)
            .Build();

        // Act
        var result = automaton.FindTransitionsFromState(1);

        // Assert
        result.ShouldContain(transition);
    }

    [Fact]
    public void FindTransitionsForSymbol_ValidSymbol_ShouldReturnTransitions()
    {
        // Arrange
        var state1 = new State { Id = 1, IsStart = true };
        var state2 = new State { Id = 2, IsStart = false };
        var transition = new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a' };
        var automaton = new TestAutomatonBuilder()
            .WithState(state1)
            .WithState(state2)
            .WithTransition(transition)
            .Build();

        // Act
        var result = automaton.FindTransitionsForSymbol('a');

        // Assert
        result.ShouldContain(transition);
    }

    [Fact]
    public void RemoveTransition_ValidTransition_ShouldRemoveTransition()
    {
        // Arrange
        var state1 = new State { Id = 1, IsStart = true };
        var state2 = new State { Id = 2, IsStart = false };
        var transition = new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a' };
        var automaton = new TestAutomatonBuilder()
            .WithState(state1)
            .WithState(state2)
            .WithTransition(transition)
            .Build();

        // Act
        automaton.RemoveTransition(transition);

        // Assert
        automaton.Transitions.ShouldNotContain(transition);
    }

    [Fact]
    public void ValidateStartState_NoStartState_ShouldThrowException()
    {
        // Arrange
        var automaton = new TestAutomatonBuilder().Build();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => automaton.ValidateStartState());
    }

    [Fact]
    public void ValidateStartState_MultipleStartStates_ShouldThrowException()
    {
        // Arrange
        var state1 = new State { Id = 1, IsStart = true };
        var state2 = new State { Id = 2, IsStart = true };
        var automaton = new TestAutomatonBuilder()
            .WithRawState(state1)
            .WithRawState(state2)
            .Build();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => automaton.ValidateStartState());
    }
}

public class TestAutomatonBuilder
{
    private readonly TestAutomaton automaton;

    public TestAutomatonBuilder()
    {
        automaton = new TestAutomaton();
    }

    public TestAutomatonBuilder WithState(State state)
    {
        automaton.AddState(state);
        return this;
    }

    public TestAutomatonBuilder WithRawState(State state)
    {
        automaton.States.Add(state);
        return this;
    }

    public TestAutomatonBuilder WithTransition(Transition transition)
    {
        automaton.AddTransition(transition);
        return this;
    }

    public TestAutomaton Build()
    {
        return automaton;
    }

    public class TestAutomaton : Automaton
    {
        public override void StepForward(AutomatonExecutionState state)
        {
            // No-op for testing
        }

        public override void StepBackward(AutomatonExecutionState state)
        {
            // No-op for testing
        }

        public override void ExecuteAll(AutomatonExecutionState state)
        {
            // No-op for testing
        }

        public new int ValidateStartState()
        {
            return base.ValidateStartState();
        }
    }
}

