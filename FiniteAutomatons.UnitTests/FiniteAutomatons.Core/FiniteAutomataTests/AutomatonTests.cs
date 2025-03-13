using FiniteAutomatons.Core.Models.DoMain;
using Shouldly;

namespace FiniteAutomatons.UnitTests.FiniteAutomatons.Core.FiniteAutomataTests;

public class AutomatonTests
{
    [Fact]
    public void AddState_ValidState_ShouldAddState()
    {
        var state = new State { Id = 1, IsStart = true };
        var automaton = new TestAutomatonBuilder()
            .WithState(state)
            .Build();

        automaton.States.ShouldContain(state);
    }

    [Fact]
    public void AddState_MultipleStartStates_ShouldThrowException()
    {
        var state1 = new State { Id = 1, IsStart = true };
        var state2 = new State { Id = 2, IsStart = true };
        var builder = new TestAutomatonBuilder()
            .WithState(state1);

        Should.Throw<InvalidOperationException>(() => builder.WithState(state2));
    }

#nullable disable
    [Fact]
    public void AddState_NullState_ShouldThrowArgumentNullException()
    {
        var automaton = new TestAutomatonBuilder().Build();

        Should.Throw<ArgumentNullException>(() => automaton.AddState(null));
    }
#nullable enable
    [Fact]
    public void SetStartState_ValidStateId_ShouldSetStartState()
    {
        var state1 = new State { Id = 1, IsStart = true };
        var state2 = new State { Id = 2, IsStart = false };
        var automaton = new TestAutomatonBuilder()
            .WithState(state1)
            .WithState(state2)
            .Build();

        automaton.SetStartState(2);

        state1.IsStart.ShouldBeFalse();
        state2.IsStart.ShouldBeTrue();
    }

    [Fact]
    public void SetStartState_InvalidStateId_ShouldThrowArgumentException()
    {
        var state1 = new State { Id = 1, IsStart = true };
        var automaton = new TestAutomatonBuilder()
            .WithState(state1)
            .Build();

        Should.Throw<ArgumentException>(() => automaton.SetStartState(2));
    }

    [Fact]
    public void AddTransition_ValidTransition_ShouldAddTransition()
    {
        var state1 = new State { Id = 1, IsStart = true };
        var state2 = new State { Id = 2, IsStart = false };
        var transition = new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a' };
        var automaton = new TestAutomatonBuilder()
            .WithState(state1)
            .WithState(state2)
            .WithTransition(transition)
            .Build();

        automaton.Transitions.ShouldContain(transition);
    }

#nullable disable
    [Fact]
    public void AddTransition_NullTransition_ShouldThrowArgumentNullException()
    {
        var automaton = new TestAutomatonBuilder().Build();

        Should.Throw<ArgumentNullException>(() => automaton.AddTransition(null));
    }
#nullable enable

    [Fact]
    public void AddTransition_InvalidFromStateId_ShouldThrowArgumentException()
    {
        var state1 = new State { Id = 1, IsStart = true };
        var state2 = new State { Id = 2, IsStart = false };
        var automaton = new TestAutomatonBuilder()
            .WithState(state1)
            .WithState(state2)
            .Build();

        var transition = new Transition { FromStateId = 3, ToStateId = 2, Symbol = 'a' };

        Should.Throw<ArgumentException>(() => automaton.AddTransition(transition));
    }

    [Fact]
    public void AddTransition_InvalidToStateId_ShouldThrowArgumentException()
    {
        var state1 = new State { Id = 1, IsStart = true };
        var state2 = new State { Id = 2, IsStart = false };
        var automaton = new TestAutomatonBuilder()
            .WithState(state1)
            .WithState(state2)
            .Build();

        var transition = new Transition { FromStateId = 1, ToStateId = 3, Symbol = 'a' };

        Should.Throw<ArgumentException>(() => automaton.AddTransition(transition));
    }

    [Fact]
    public void FindTransitionsFromState_ValidStateId_ShouldReturnTransitions()
    {
        var state1 = new State { Id = 1, IsStart = true };
        var state2 = new State { Id = 2, IsStart = false };
        var transition = new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a' };
        var automaton = new TestAutomatonBuilder()
            .WithState(state1)
            .WithState(state2)
            .WithTransition(transition)
            .Build();

        var transitions = automaton.FindTransitionsFromState(1);

        transitions.ShouldContain(transition);
    }

    [Fact]
    public void FindTransitionsForSymbol_ValidSymbol_ShouldReturnTransitions()
    {
        var state1 = new State { Id = 1, IsStart = true };
        var state2 = new State { Id = 2, IsStart = false };
        var transition = new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a' };
        var automaton = new TestAutomatonBuilder()
            .WithState(state1)
            .WithState(state2)
            .WithTransition(transition)
            .Build();

        var transitions = automaton.FindTransitionsForSymbol('a');

        transitions.ShouldContain(transition);
    }

    [Fact]
    public void RemoveTransition_ValidTransition_ShouldRemoveTransition()
    {
        var state1 = new State { Id = 1, IsStart = true };
        var state2 = new State { Id = 2, IsStart = false };
        var transition = new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a' };
        var automaton = new TestAutomatonBuilder()
            .WithState(state1)
            .WithState(state2)
            .WithTransition(transition)
            .Build();

        automaton.RemoveTransition(transition);

        automaton.Transitions.ShouldNotContain(transition);
    }

    [Fact]
    public void ValidateStartState_NoStartState_ShouldThrowException()
    {
        var automaton = new TestAutomatonBuilder().Build();

        Should.Throw<InvalidOperationException>(() => automaton.ValidateStartState());
    }

    [Fact]
    public void ValidateStartState_MultipleStartStates_ShouldThrowException()
    {
        var state1 = new State { Id = 1, IsStart = true };
        var state2 = new State { Id = 2, IsStart = true };
        var automaton = new TestAutomatonBuilder()
            .WithRawState(state1)
            .WithRawState(state2)
            .Build();

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
        public override bool Execute(string input)
        {
            return true;
        }

        public new int ValidateStartState()
        {
            return base.ValidateStartState();
        }
    }
}
