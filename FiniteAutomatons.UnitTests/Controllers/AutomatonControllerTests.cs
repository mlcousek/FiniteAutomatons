using FiniteAutomatons.Controllers;
using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Shouldly;

namespace FiniteAutomatons.UnitTests.Controllers;

public class AutomatonControllerTests
{
    private readonly AutomatonCreationController controller;

    public AutomatonControllerTests()
    {
        var logger = new TestLogger<AutomatonCreationController>();
        var mockTempDataService = new MockAutomatonTempDataService();
        var mockValidationService = new MockAutomatonValidationService();
        var mockEditingService = new AutomatonEditingService(new MockAutomatonValidationService(), new TestLogger<AutomatonEditingService>());

        controller = new AutomatonCreationController(logger, mockTempDataService,
            mockValidationService, mockEditingService, new MockAutomatonMinimizationService());

        var httpContext = new DefaultHttpContext();
        var tempData = new TempDataDictionary(httpContext, new TestTempDataProvider());
        controller.TempData = tempData;
    }

    [Fact]
    public void CreateAutomaton_Get_ReturnsEmptyModel()
    {
        // Act
        var result = controller.CreateAutomaton() as ViewResult;

        // Assert
        result.ShouldNotBeNull();
        var model = result.Model as AutomatonViewModel;
        model.ShouldNotBeNull();
        model.States.ShouldBeEmpty();
        model.Transitions.ShouldBeEmpty();
        model.Alphabet.ShouldBeEmpty();
    }

    [Fact]
    public void AddState_ValidState_AddsStateToModel()
    {
        // Arrange
        var model = new AutomatonViewModel();

        // Act
        var result = controller.AddState(model, 1, true, false) as ViewResult;

        // Assert
        result.ShouldNotBeNull();
        result.ViewName.ShouldBe("CreateAutomaton");
        var returnedModel = result.Model as AutomatonViewModel;
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
        var model = new AutomatonViewModel();
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
    public void CreateAutomaton_ValidModel_RedirectsToIndex()
    {
        // Arrange
        var model = new AutomatonViewModel();
        model.States.Add(new State { Id = 1, IsStart = true, IsAccepting = false });
        model.States.Add(new State { Id = 2, IsStart = false, IsAccepting = true });
        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a' });

        // Act
        var result = controller.CreateAutomaton(model) as RedirectToActionResult;

        // Assert
        result.ShouldNotBeNull();
        result.ActionName.ShouldBe("Index");
        result.ControllerName.ShouldBe("Home");
        controller.TempData["CustomAutomaton"].ShouldNotBeNull();
    }
}
