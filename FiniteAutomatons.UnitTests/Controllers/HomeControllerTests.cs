using FiniteAutomatons.Controllers;
using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace FiniteAutomatons.UnitTests.Controllers;

public class HomeControllerTests
{
    private readonly HomeController controller;

    public HomeControllerTests()
    {
        var logger = new TestLogger<HomeController>();
        controller = new HomeController(logger);

        // Setup TempData
        var httpContext = new DefaultHttpContext();
        var tempData = new TempDataDictionary(httpContext, new TestTempDataProvider());
        controller.TempData = tempData;
    }

    [Fact]
    public void Index_WithoutCustomAutomaton_ReturnsDefaultDfa()
    {
        // Act
        var result = controller.Index() as ViewResult;

        // Assert
        result.ShouldNotBeNull();
        var model = result.Model as DfaViewModel;
        model.ShouldNotBeNull();
        model.States.Count.ShouldBe(5);
        model.States.Count(s => s.IsStart).ShouldBe(1);
        model.States.Count(s => s.IsAccepting).ShouldBe(1);
        model.Alphabet.ShouldContain('a');
        model.Alphabet.ShouldContain('b');
        model.Alphabet.ShouldContain('c');
    }

    [Fact]
    public void CreateAutomaton_Get_ReturnsEmptyModel()
    {
        // Act
        var result = controller.CreateAutomaton() as ViewResult;

        // Assert
        result.ShouldNotBeNull();
        var model = result.Model as DfaViewModel;
        model.ShouldNotBeNull();
        model.States.ShouldBeEmpty();
        model.Transitions.ShouldBeEmpty();
        model.Alphabet.ShouldBeEmpty();
    }

    [Fact]
    public void AddState_ValidState_AddsStateToModel()
    {
        // Arrange
        var model = new DfaViewModel();

        // Act
        var result = controller.AddState(model, 1, true, false) as ViewResult;

        // Assert
        result.ShouldNotBeNull();
        result.ViewName.ShouldBe("CreateAutomaton");
        var returnedModel = result.Model as DfaViewModel;
        returnedModel.ShouldNotBeNull();
        returnedModel.States.Count.ShouldBe(1);
        returnedModel.States[0].Id.ShouldBe(1);
        returnedModel.States[0].IsStart.ShouldBeTrue();
        returnedModel.States[0].IsAccepting.ShouldBeFalse();
    }

    [Fact]
    public void AddState_DuplicateStateId_ReturnsModelStateError()
    {
        // Arrange
        var model = new DfaViewModel();
        model.States.Add(new State { Id = 1, IsStart = true, IsAccepting = false });

        // Act
        var result = controller.AddState(model, 1, false, true) as ViewResult;

        // Assert
        result.ShouldNotBeNull();
        controller.ModelState.IsValid.ShouldBeFalse();
        controller.ModelState.ErrorCount.ShouldBe(1);
        var errors = controller.ModelState[""]?.Errors;
        errors.ShouldNotBeNull();
        errors[0].ErrorMessage.ShouldContain("State with ID 1 already exists");
    }

    [Fact]
    public void AddState_MultipleStartStates_ReturnsModelStateError()
    {
        // Arrange
        var model = new DfaViewModel();
        model.States.Add(new State { Id = 1, IsStart = true, IsAccepting = false });

        // Act
        var result = controller.AddState(model, 2, true, false) as ViewResult;

        // Assert
        result.ShouldNotBeNull();
        controller.ModelState.IsValid.ShouldBeFalse();
        controller.ModelState.ErrorCount.ShouldBe(1);
        var errors = controller.ModelState[""]?.Errors;
        errors.ShouldNotBeNull();
        errors[0].ErrorMessage.ShouldContain("Only one start state is allowed");
    }

    [Fact]
    public void AddTransition_ValidTransition_AddsTransitionAndUpdatesAlphabet()
    {
        // Arrange
        var model = new DfaViewModel();
        model.States.Add(new State { Id = 1, IsStart = true, IsAccepting = false });
        model.States.Add(new State { Id = 2, IsStart = false, IsAccepting = true });

        // Act
        var result = controller.AddTransition(model, 1, 2, 'a') as ViewResult;

        // Assert
        result.ShouldNotBeNull();
        result.ViewName.ShouldBe("CreateAutomaton");
        var returnedModel = result.Model as DfaViewModel;
        returnedModel.ShouldNotBeNull();
        returnedModel.Transitions.Count.ShouldBe(1);
        returnedModel.Transitions[0].FromStateId.ShouldBe(1);
        returnedModel.Transitions[0].ToStateId.ShouldBe(2);
        returnedModel.Transitions[0].Symbol.ShouldBe('a');
        returnedModel.Alphabet.ShouldContain('a');
    }

    [Fact]
    public void AddTransition_NonExistentFromState_ReturnsModelStateError()
    {
        // Arrange
        var model = new DfaViewModel();
        model.States.Add(new State { Id = 2, IsStart = false, IsAccepting = true });

        // Act
        var result = controller.AddTransition(model, 1, 2, 'a') as ViewResult;

        // Assert
        result.ShouldNotBeNull();
        controller.ModelState.IsValid.ShouldBeFalse();
        var errors = controller.ModelState[""]?.Errors;
        errors.ShouldNotBeNull();
        errors[0].ErrorMessage.ShouldContain("From state 1 does not exist");
    }

    [Fact]
    public void AddTransition_NonExistentToState_ReturnsModelStateError()
    {
        // Arrange
        var model = new DfaViewModel();
        model.States.Add(new State { Id = 1, IsStart = true, IsAccepting = false });

        // Act
        var result = controller.AddTransition(model, 1, 2, 'a') as ViewResult;

        // Assert
        result.ShouldNotBeNull();
        controller.ModelState.IsValid.ShouldBeFalse();
        var errors = controller.ModelState[""]?.Errors;
        errors.ShouldNotBeNull();
        errors[0].ErrorMessage.ShouldContain("To state 2 does not exist");
    }

    [Fact]
    public void AddTransition_DuplicateTransition_ReturnsModelStateError()
    {
        // Arrange
        var model = new DfaViewModel();
        model.States.Add(new State { Id = 1, IsStart = true, IsAccepting = false });
        model.States.Add(new State { Id = 2, IsStart = false, IsAccepting = true });
        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a' });

        // Act
        var result = controller.AddTransition(model, 1, 2, 'a') as ViewResult;

        // Assert
        result.ShouldNotBeNull();
        controller.ModelState.IsValid.ShouldBeFalse();
        var errors = controller.ModelState[""]?.Errors;
        errors.ShouldNotBeNull();
        errors[0].ErrorMessage.ShouldContain("Transition from 1 to 2 on 'a' already exists");
    }

    [Fact]
    public void RemoveState_ValidState_RemovesStateAndRelatedTransitions()
    {
        // Arrange
        var model = new DfaViewModel();
        model.States.Add(new State { Id = 1, IsStart = true, IsAccepting = false });
        model.States.Add(new State { Id = 2, IsStart = false, IsAccepting = true });
        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a' });
        model.Alphabet.Add('a');

        // Act
        var result = controller.RemoveState(model, 1) as ViewResult;

        // Assert
        result.ShouldNotBeNull();
        var returnedModel = result.Model as DfaViewModel;
        returnedModel.ShouldNotBeNull();
        returnedModel.States.Count.ShouldBe(1);
        returnedModel.States.ShouldNotContain(s => s.Id == 1);
        returnedModel.Transitions.ShouldBeEmpty();
        returnedModel.Alphabet.ShouldBeEmpty(); // Should be removed since no transitions use it
    }

    [Fact]
    public void RemoveTransition_ValidTransition_RemovesTransitionAndUpdatesAlphabet()
    {
        // Arrange
        var model = new DfaViewModel();
        model.States.Add(new State { Id = 1, IsStart = true, IsAccepting = false });
        model.States.Add(new State { Id = 2, IsStart = false, IsAccepting = true });
        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a' });
        model.Alphabet.Add('a');

        // Act
        var result = controller.RemoveTransition(model, 1, 2, 'a') as ViewResult;

        // Assert
        result.ShouldNotBeNull();
        var returnedModel = result.Model as DfaViewModel;
        returnedModel.ShouldNotBeNull();
        returnedModel.Transitions.ShouldBeEmpty();
        returnedModel.Alphabet.ShouldBeEmpty(); // Should be removed since no transitions use it
    }

    [Fact]
    public void CreateAutomaton_ValidModel_RedirectsToIndex()
    {
        // Arrange
        var model = new DfaViewModel();
        model.States.Add(new State { Id = 1, IsStart = true, IsAccepting = false });
        model.States.Add(new State { Id = 2, IsStart = false, IsAccepting = true });
        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a' });

        // Act
        var result = controller.CreateAutomaton(model) as RedirectToActionResult;

        // Assert
        result.ShouldNotBeNull();
        result.ActionName.ShouldBe("Index");
        controller.TempData["CustomAutomaton"].ShouldNotBeNull();
    }

    [Fact]
    public void CreateAutomaton_InvalidModel_ReturnsViewWithErrors()
    {
        // Arrange
        var model = new DfaViewModel(); // Empty model - no states

        // Act
        var result = controller.CreateAutomaton(model) as ViewResult;

        // Assert
        result.ShouldNotBeNull();
        controller.ModelState.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void ValidateAutomaton_NoStates_ReturnsFalse()
    {
        // Arrange
        var model = new DfaViewModel();

        // Act
        var result = controller.CreateAutomaton(model) as ViewResult;

        // Assert
        result.ShouldNotBeNull();
        controller.ModelState.IsValid.ShouldBeFalse();
        var errors = controller.ModelState[""]?.Errors;
        errors.ShouldNotBeNull();
        errors[0].ErrorMessage.ShouldContain("Automaton must have at least one state");
    }

    [Fact]
    public void ValidateAutomaton_NoStartState_ReturnsFalse()
    {
        // Arrange
        var model = new DfaViewModel();
        model.States.Add(new State { Id = 1, IsStart = false, IsAccepting = true });

        // Act
        var result = controller.CreateAutomaton(model) as ViewResult;

        // Assert
        result.ShouldNotBeNull();
        controller.ModelState.IsValid.ShouldBeFalse();
        var errors = controller.ModelState[""]?.Errors;
        errors.ShouldNotBeNull();
        errors[0].ErrorMessage.ShouldContain("Automaton must have exactly one start state");
    }

    [Fact]
    public void ValidateAutomaton_MultipleStartStates_ReturnsFalse()
    {
        // Arrange
        var model = new DfaViewModel();
        model.States.Add(new State { Id = 1, IsStart = true, IsAccepting = false });
        model.States.Add(new State { Id = 2, IsStart = true, IsAccepting = true });

        // Act
        var result = controller.CreateAutomaton(model) as ViewResult;

        // Assert
        result.ShouldNotBeNull();
        controller.ModelState.IsValid.ShouldBeFalse();
        var errors = controller.ModelState[""]?.Errors;
        errors.ShouldNotBeNull();
        errors[0].ErrorMessage.ShouldContain("Automaton must have exactly one start state");
    }
}

// Test helper classes
public class TestLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return false;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
}

public class TestTempDataProvider : ITempDataProvider
{
    private readonly Dictionary<string, object?> _data = [];

    public IDictionary<string, object?> LoadTempData(HttpContext context)
    {
        return _data;
    }

    public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
    {
        _data.Clear();
        foreach (var kvp in values)
        {
            _data[kvp.Key] = kvp.Value;
        }
    }
}
