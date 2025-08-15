using System;
using System.Collections.Generic;
using System.Linq;
using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Services;
using FiniteAutomatons.UnitTests.FiniteAutomatons.Core.FiniteAutomataTests; // for DFABuilder
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace FiniteAutomatons.UnitTests.FiniteAutomatons.Core.AdditionalTests;

public class AutomatonBuilderAndDFASupplementalTests
{
    private static AutomatonViewModel BaseModel(AutomatonType type) => new()
    {
        Type = type,
        States = new List<State>(),
        Transitions = new List<Transition>(),
        Alphabet = new List<char>()
    };

    // Local minimal DFA builder (duplicated lightweight for isolation)
    private class LocalDfaBuilder
    {
        private readonly DFA _dfa = new();
        public LocalDfaBuilder WithState(int id, bool isStart = false, bool isAccepting = false)
        { _dfa.AddState(new State { Id = id, IsStart = isStart, IsAccepting = isAccepting }); return this; }
        public LocalDfaBuilder WithTransition(int from, int to, char symbol)
        { _dfa.AddTransition(from, to, symbol); return this; }
        public DFA Build() => _dfa;
    }

    [Fact]
    public void CreateAutomatonFromModel_DFA_BuildsCorrectAutomaton()
    {
        var model = BaseModel(AutomatonType.DFA);
        model.States.AddRange(new[]
        {
            new State { Id = 1, IsStart = true, IsAccepting = false },
            new State { Id = 2, IsStart = false, IsAccepting = true }
        });
        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a' });
        model.Alphabet.Add('a');

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
        model.States.AddRange(new[]
        {
            new State { Id = 1, IsStart = true, IsAccepting = false },
            new State { Id = 2, IsStart = false, IsAccepting = true }
        });
        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a' });
        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a' }); // nondeterminism same symbol to two targets
        model.Alphabet.Add('a');

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
        model.States.AddRange(new[]
        {
            new State { Id = 1, IsStart = true, IsAccepting = false },
            new State { Id = 2, IsStart = false, IsAccepting = true }
        });
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
        model.States.AddRange(new[]
        {
            new State { Id = 1, IsStart = true, IsAccepting = false },
            new State { Id = 2, IsStart = true, IsAccepting = true }
        });

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
        dfa.StepForward(exec); // push history, move to state 2, pos1
        exec.StateHistory.Clear(); // simulate lost history

        // StepBackward triggers fallback recomputation (history empty, position>0)
        dfa.StepBackward(exec);
        exec.Position.ShouldBe(0);
        exec.CurrentStateId.ShouldBe(1); // recomputed from input prefix length 0
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
