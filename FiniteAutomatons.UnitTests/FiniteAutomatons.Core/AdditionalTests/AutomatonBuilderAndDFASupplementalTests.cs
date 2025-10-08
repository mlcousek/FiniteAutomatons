using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FiniteAutomatons.UnitTests.FiniteAutomatons.Core.AdditionalTests;

public class AutomatonBuilderAndDFASupplementalTests
{
    private static AutomatonViewModel BaseModel(AutomatonType type) => new()
    {
        Type = type,
        States = [],
        Transitions = [],
    };

    private class LocalDfaBuilder
    {
        private readonly DFA dfa = new();
        public LocalDfaBuilder WithState(int id, bool isStart = false, bool isAccepting = false)
        { dfa.AddState(new State { Id = id, IsStart = isStart, IsAccepting = isAccepting }); return this; }
        public LocalDfaBuilder WithTransition(int from, int to, char symbol)
        { dfa.AddTransition(from, to, symbol); return this; }
        public DFA Build() => dfa;
    }

    [Fact]
    public void CreateAutomatonFromModel_DFA_BuildsCorrectAutomaton()
    {
        var model = BaseModel(AutomatonType.DFA);
        model.States.AddRange(
        [
            new State { Id = 1, IsStart = true, IsAccepting = false },
            new State { Id = 2, IsStart = false, IsAccepting = true }
        ]);
        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a' });

        var svc = new AutomatonBuilderService(new NullLogger<AutomatonBuilderService>());
        var automaton = svc.CreateAutomatonFromModel(model) as DFA;
        automaton.ShouldNotBeNull();
        automaton.StartStateId.ShouldBe(1);
        automaton.Transitions.Count.ShouldBe(1);
    }

    [Fact]
    public void CreateAutomatonFromModel_NFA_BuildsCorrectAutomaton()
    {
        var model = BaseModel(AutomatonType.NFA);
        model.States.AddRange(
        [
            new State { Id = 1, IsStart = true, IsAccepting = false },
            new State { Id = 2, IsStart = false, IsAccepting = true }
        ]);
        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a' });
        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a' }); 

        var svc = new AutomatonBuilderService(new NullLogger<AutomatonBuilderService>());
        var automaton = svc.CreateAutomatonFromModel(model) as NFA;
        automaton.ShouldNotBeNull();
        automaton.StartStateId.ShouldBe(1);
        automaton.Transitions.Count.ShouldBe(2);
    }

    [Fact]
    public void CreateAutomatonFromModel_EpsilonNFA_PreservesEpsilonTransitions()
    {
        var model = BaseModel(AutomatonType.EpsilonNFA);
        model.States.AddRange(
        [
            new State { Id = 1, IsStart = true, IsAccepting = false },
            new State { Id = 2, IsStart = false, IsAccepting = true }
        ]);
        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 2, Symbol = '\0' }); // epsilon internal

        var svc = new AutomatonBuilderService(new NullLogger<AutomatonBuilderService>());
        var automaton = svc.CreateAutomatonFromModel(model) as EpsilonNFA;
        automaton.ShouldNotBeNull();
        automaton.Transitions.Any(t => t.Symbol == '\0').ShouldBeTrue();
    }

    [Fact]
    public void CreateAutomatonFromModel_MultipleStartStates_Throws()
    {
        var model = BaseModel(AutomatonType.DFA);
        model.States.AddRange(
        [
            new State { Id = 1, IsStart = true, IsAccepting = false },
            new State { Id = 2, IsStart = true, IsAccepting = true }
        ]);

        var svc = new AutomatonBuilderService(new NullLogger<AutomatonBuilderService>());
        Should.Throw<InvalidOperationException>(() => svc.CreateAutomatonFromModel(model));
    }

    [Fact]
    public void CreateAutomatonFromModel_EmptyCollections_Succeeds()
    {
        var model = BaseModel(AutomatonType.DFA);
        var svc = new AutomatonBuilderService(new NullLogger<AutomatonBuilderService>());
        var automaton = svc.CreateAutomatonFromModel(model);
        automaton.States.Count.ShouldBe(0);
        automaton.Transitions.Count.ShouldBe(0);
    }

    [Fact]
    public void DFA_StepBackward_FallbackRecomputesWhenHistoryMissing()
    {
        var dfa = new LocalDfaBuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        var exec = dfa.StartExecution("a");
        dfa.StepForward(exec); 
        exec.StateHistory.Clear(); 

        dfa.StepBackward(exec);
        exec.Position.ShouldBe(0);
        exec.CurrentStateId.ShouldBe(1); 
        exec.IsAccepted.ShouldBeNull();
    }

    [Fact]
    public void MinimalizeDFA_NoStartState_Throws()
    {
        var dfa = new LocalDfaBuilder()
            .WithState(1, isStart: false, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        Should.Throw<InvalidOperationException>(() => dfa.MinimalizeDFA());
    }
}
