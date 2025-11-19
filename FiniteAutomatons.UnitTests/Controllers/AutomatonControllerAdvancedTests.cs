using FiniteAutomatons.Controllers;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Core.Utilities;
using FiniteAutomatons.Services.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Shouldly;

namespace FiniteAutomatons.UnitTests.Controllers;

public class AutomatonControllerAdvancedTests   //TODO refactore tests, introduce builders and edit mocks
{
    private readonly AutomatonController controllerWithRealValidation;
    private readonly AutomatonController controllerWithMocks;

    public AutomatonControllerAdvancedTests()
    {
        var realValidation = new AutomatonValidationService(new TestLogger<AutomatonValidationService>());
        var mockGenerator = new MockAutomatonGeneratorService();
        var mockTempData = new MockAutomatonTempDataService();
        var mockConversion = new MockAutomatonConversionService();
        var mockExecution = new MockAutomatonExecutionService();
        var editingService = new AutomatonEditingService(realValidation, new TestLogger<AutomatonEditingService>());

        controllerWithRealValidation = new AutomatonController(new TestLogger<AutomatonController>(), mockGenerator, mockTempData, realValidation, mockConversion, mockExecution, editingService, new MockAutomatonFileService(), new MockAutomatonMinimizationService())
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider())
        };

        var mockEditing = new AutomatonEditingService(new MockAutomatonValidationService(), new TestLogger<AutomatonEditingService>());
        controllerWithMocks = new AutomatonController(new TestLogger<AutomatonController>(), mockGenerator, mockTempData, new MockAutomatonValidationService(), mockConversion, mockExecution, mockEditing, new MockAutomatonFileService(), new MockAutomatonMinimizationService())
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider())
        };
    }

    public static IEnumerable<object[]> EpsilonAcceptedAliases() =>
    [
        ["epsilon"],
        ["eps"],
        ["lambda"],
        ["\\0"], 
        ["\0"]    
    ];

    public static IEnumerable<object[]> EpsilonRejectedAliases() =>
    [
        ["epsilon"]
    ];

    [Fact]
    public void ChangeAutomatonType_ClearsExecutionState()
    {
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [ new() { Id = 1, IsStart = true, IsAccepting = false } ],
            Input = "abc",
            Position = 2,
            CurrentStateId = 1,
            Result = true,
            IsAccepted = true,
            StateHistorySerialized = "[ [1] ]"
        };

        var result = controllerWithMocks.ChangeAutomatonType(model, AutomatonType.NFA) as ViewResult;
        var vm = result!.Model as AutomatonViewModel;
        vm!.Type.ShouldBe(AutomatonType.NFA);
        vm.Input.ShouldBe(string.Empty);
        vm.Position.ShouldBe(0);
        vm.CurrentStateId.ShouldBeNull();
        vm.CurrentStates.ShouldBeNull();
        vm.Result.ShouldBeNull();
        vm.IsAccepted.ShouldBeNull();
        vm.StateHistorySerialized.ShouldBe(string.Empty);
    }

    [Fact]
    public void ConvertToDFA_ClearsExecutionState()
    {
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States = [ new() { Id = 1, IsStart = true, IsAccepting = false } ],
            Input = "test",
            Position = 1,
            CurrentStates = [1],
            Result = false,
            IsAccepted = false
        };

        var result = controllerWithMocks.ConvertToDFA(model) as RedirectToActionResult;
        result.ShouldNotBeNull();
    }

    [Theory]
    [MemberData(nameof(EpsilonAcceptedAliases))]
    public void AddTransition_EpsilonAliases_AcceptedForEpsilonNFA(string alias)
    {
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.EpsilonNFA,
            States = [ new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true } ]
        };

        var result = controllerWithRealValidation.AddTransition(model, 1, 2, alias) as ViewResult;
        var vm = result!.Model as AutomatonViewModel;
        vm!.Transitions.Count.ShouldBe(1, customMessage: $"Alias '{alias}' should produce one epsilon transition");
        vm.Transitions[0].Symbol.ShouldBe(AutomatonSymbolHelper.EpsilonInternal);
        vm.Alphabet.ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(EpsilonRejectedAliases))]
    public void AddTransition_EpsilonAlias_RejectedForDFA(string alias)
    {
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [ new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true } ]
        };

        var result = controllerWithRealValidation.AddTransition(model, 1, 2, alias) as ViewResult;
        var vm = result!.Model as AutomatonViewModel;
        vm!.Transitions.ShouldBeEmpty();
        controllerWithRealValidation.ModelState.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void RemoveTransition_NoMatch_AddsModelError()
    {
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true }],
            Transitions = [new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }],
        };

        var result = controllerWithMocks.RemoveTransition(model, 1, 2, "b") as ViewResult;
        controllerWithMocks.ModelState.IsValid.ShouldBeFalse();
        var vm = result!.Model as AutomatonViewModel;
        vm!.Transitions.Count.ShouldBe(1);
    }

    [Fact]
    public void RemoveTransition_InvalidSymbolFormat_AddsModelError()
    {
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true }]
        };

        controllerWithMocks.RemoveTransition(model, 1, 2, "invalidSymbol");
        controllerWithMocks.ModelState.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void AddTransition_DuplicateForDFA_ShouldBeRejected()
    {
        var realValidation = new AutomatonValidationService(new TestLogger<AutomatonValidationService>());
        var controller = new AutomatonController(new TestLogger<AutomatonController>(), new MockAutomatonGeneratorService(), new MockAutomatonTempDataService(), realValidation, new MockAutomatonConversionService(), new MockAutomatonExecutionService(), new AutomatonEditingService(realValidation, new TestLogger<AutomatonEditingService>()), new MockAutomatonFileService(), new MockAutomatonMinimizationService())
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider())
        };
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true }],
            Transitions = [new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }],
        };

        controller.AddTransition(model, 1, 2, "a");
        controller.ModelState.IsValid.ShouldBeFalse();
        model.Transitions.Count.ShouldBe(1);
    }
}
