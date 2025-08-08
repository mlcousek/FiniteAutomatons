using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Controllers;
using FiniteAutomatons.Services.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace FiniteAutomatons.UnitTests.Controllers;

public class AutomatonControllerResetTests
{
    private readonly AutomatonController _controller;

    public AutomatonControllerResetTests()
    {
        // Create a simple logger for testing
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<AutomatonController>();
        var mockGeneratorService = new MockAutomatonGeneratorService();
        var mockTempDataService = new MockAutomatonTempDataService();
        var mockValidationService = new MockAutomatonValidationService();
        var mockConversionService = new MockAutomatonConversionService();
        var mockExecutionService = new MockAutomatonExecutionService();
        
        _controller = new AutomatonController(logger, mockGeneratorService, mockTempDataService,
            mockValidationService, mockConversionService, mockExecutionService);
    }

    [Fact]
    public void Reset_ShouldPreserveAutomatonStructure_AndClearExecutionState()
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
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 2, ToStateId = 1, Symbol = 'b' }
            ],
            Alphabet = ['a', 'b'],
            Input = "test input",
            Position = 3,
            CurrentStateId = 2,
            Result = true,
            IsAccepted = true,
            StateHistorySerialized = "some history"
        };

        // Act
        var result = _controller.Reset(model);

        // Assert
        result.ShouldBeOfType<ViewResult>();
        var viewResult = (ViewResult)result;
        var resultModel = viewResult.Model as AutomatonViewModel;

        resultModel.ShouldNotBeNull();
        
        // Automaton structure should be preserved
        resultModel.Type.ShouldBe(AutomatonType.DFA);
        resultModel.States.Count.ShouldBe(2);
        resultModel.Transitions.Count.ShouldBe(2);
        resultModel.Alphabet.Count.ShouldBe(2);
        resultModel.Alphabet.ShouldContain('a');
        resultModel.Alphabet.ShouldContain('b');

        // Execution state should be cleared
        resultModel.Input.ShouldBe(string.Empty);
        resultModel.Position.ShouldBe(0);
        resultModel.CurrentStateId.ShouldBeNull();
        resultModel.Result.ShouldBeNull();
        resultModel.IsAccepted.ShouldBeNull();
        resultModel.StateHistorySerialized.ShouldBe(string.Empty);
    }

    [Fact]
    public void Reset_WithEpsilonNFA_ShouldPreserveEpsilonTransitions()
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
                new() { FromStateId = 1, ToStateId = 2, Symbol = '\0' }, // Epsilon transition
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }
            ],
            Alphabet = ['a'],
            Input = "test",
            Position = 2,
            CurrentStates = [1, 2]
        };

        // Act
        var result = _controller.Reset(model);

        // Assert
        result.ShouldBeOfType<ViewResult>();
        var viewResult = (ViewResult)result;
        var resultModel = viewResult.Model as AutomatonViewModel;

        resultModel.ShouldNotBeNull();
        
        // Should preserve transitions including epsilon
        resultModel.Transitions.Count.ShouldBe(2);
        resultModel.Transitions.ShouldContain(t => t.Symbol == '\0');
        resultModel.Transitions.ShouldContain(t => t.Symbol == 'a');
        
        // Alphabet should only contain non-epsilon symbols
        resultModel.Alphabet.ShouldContain('a');
        resultModel.Alphabet.ShouldNotContain('\0');

        // Execution state should be cleared
        resultModel.Input.ShouldBe(string.Empty);
        resultModel.Position.ShouldBe(0);
        resultModel.CurrentStates.ShouldBeNull();
    }

    [Fact]
    public void Reset_WithNoInput_ShouldStillWork()
    {
        // Arrange
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = true }
            ],
            Transitions = [],
            Alphabet = [],
            Input = string.Empty,
            Position = 0
        };

        // Act
        var result = _controller.Reset(model);

        // Assert
        result.ShouldBeOfType<ViewResult>();
        var viewResult = (ViewResult)result;
        var resultModel = viewResult.Model as AutomatonViewModel;

        resultModel.ShouldNotBeNull();
        resultModel.States.Count.ShouldBe(1);
        resultModel.Input.ShouldBe(string.Empty);
        resultModel.Position.ShouldBe(0);
    }
}
