using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Controllers;
using FiniteAutomatons.Services.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace FiniteAutomatons.UnitTests.Controllers;

public class AutomatonControllerResetTests
{
    private readonly AutomatonController controller;

    public AutomatonControllerResetTests()
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<AutomatonController>();
        var mockGeneratorService = new MockAutomatonGeneratorService();
        var mockTempDataService = new MockAutomatonTempDataService();
        var mockValidationService = new MockAutomatonValidationService();
        var mockConversionService = new MockAutomatonConversionService();
        var mockExecutionService = new MockAutomatonExecutionService();
        var editing = new AutomatonEditingService(new MockAutomatonValidationService(), new TestLogger<AutomatonEditingService>());
        controller = new AutomatonController(logger, mockGeneratorService, mockTempDataService,
            mockValidationService, mockConversionService, mockExecutionService, editing)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider())
        };
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
            Input = "test input",
            Position = 3,
            CurrentStateId = 2,
            Result = true,
            IsAccepted = true,
            StateHistorySerialized = "some history"
        };

        // Act
        var result = controller.Reset(model);

        // Assert
        result.ShouldBeOfType<ViewResult>();
        var viewResult = (ViewResult)result;
        var resultModel = viewResult.Model as AutomatonViewModel;

        resultModel.ShouldNotBeNull();
        
        resultModel.Type.ShouldBe(AutomatonType.DFA);
        resultModel.States.Count.ShouldBe(2);
        resultModel.Transitions.Count.ShouldBe(2);
        resultModel.Alphabet.Count.ShouldBe(2);
        resultModel.Alphabet.ShouldContain('a');
        resultModel.Alphabet.ShouldContain('b');

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
                new() { FromStateId = 1, ToStateId = 2, Symbol = '\0' },
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }
            ],
            Input = "test",
            Position = 2,
            CurrentStates = [1, 2]
        };

        // Act
        var result = controller.Reset(model);

        // Assert
        result.ShouldBeOfType<ViewResult>();
        var viewResult = (ViewResult)result;
        var resultModel = viewResult.Model as AutomatonViewModel;

        resultModel.ShouldNotBeNull();
        
        resultModel.Transitions.Count.ShouldBe(2);
        resultModel.Transitions.ShouldContain(t => t.Symbol == '\0');
        resultModel.Transitions.ShouldContain(t => t.Symbol == 'a');
        
        resultModel.Alphabet.ShouldContain('a');
        resultModel.Alphabet.ShouldNotContain('\0');

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
            Input = string.Empty,
            Position = 0
        };

        // Act
        var result = controller.Reset(model);

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
