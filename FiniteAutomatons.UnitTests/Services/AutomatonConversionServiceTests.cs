using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using FiniteAutomatons.Services.Services;
using FiniteAutomatons.UnitTests.Controllers;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace FiniteAutomatons.UnitTests.Services;

public class AutomatonConversionServiceTests
{
    private readonly AutomatonConversionService _service;

    public AutomatonConversionServiceTests()
    {
        var mockBuilderService = new MockAutomatonBuilderService();
        var logger = new TestLogger<AutomatonConversionService>();
        _service = new AutomatonConversionService(mockBuilderService, logger);
    }

    [Fact]
    public void ConvertAutomatonType_DFAToNFA_ShouldSucceed()
    {
        // Arrange
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }
            ]
        };

        // Act
        var (convertedModel, warnings) = _service.ConvertAutomatonType(model, AutomatonType.NFA);

        // Assert
        convertedModel.Type.ShouldBe(AutomatonType.NFA);
        convertedModel.States.Count.ShouldBe(2);
        convertedModel.Transitions.Count.ShouldBe(1);
        warnings.ShouldBeEmpty();
    }

    [Fact]
    public void ConvertAutomatonType_EpsilonNFAToNFA_ShouldRemoveEpsilonTransitions()
    {
        // Arrange
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.EpsilonNFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 1, ToStateId = 2, Symbol = '\0' } // Epsilon transition
            ]
        };

        // Act
        var (convertedModel, warnings) = _service.ConvertAutomatonType(model, AutomatonType.NFA);

        // Assert
        convertedModel.Type.ShouldBe(AutomatonType.NFA);
        convertedModel.Transitions.Count.ShouldBe(1);
        convertedModel.Transitions.ShouldNotContain(t => t.Symbol == '\0');
        warnings.ShouldContain("Epsilon transitions have been removed during conversion to NFA.");
    }

    [Fact]
    public void ConvertToDFA_AlreadyDFA_ShouldReturnOriginal()
    {
        // Arrange
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = true }
            ]
        };

        // Act
        var result = _service.ConvertToDFA(model);

        // Assert
        result.ShouldBe(model);
    }
}

public class MockAutomatonBuilderService : IAutomatonBuilderService
{
    public Automaton CreateAutomatonFromModel(AutomatonViewModel model)
    {
        // Mock implementation - return a DFA for simplicity
        var dfa = new DFA();
        foreach (var state in model.States ?? [])
        {
            dfa.States.Add(state);
        }
        foreach (var transition in model.Transitions ?? [])
        {
            dfa.Transitions.Add(transition);
        }
        return dfa;
    }

    public DFA CreateDFA(AutomatonViewModel model)
    {
        var dfa = new DFA();
        foreach (var state in model.States ?? [])
        {
            dfa.States.Add(state);
        }
        return dfa;
    }

    public NFA CreateNFA(AutomatonViewModel model)
    {
        var nfa = new NFA();
        foreach (var state in model.States ?? [])
        {
            nfa.States.Add(state);
        }
        return nfa;
    }

    public EpsilonNFA CreateEpsilonNFA(AutomatonViewModel model)
    {
        var enfa = new EpsilonNFA();
        foreach (var state in model.States ?? [])
        {
            enfa.States.Add(state);
        }
        return enfa;
    }
}