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
    private readonly AutomatonCreationController controllerWithRealValidation;
    private readonly AutomatonCreationController controllerWithMocks;

    public AutomatonControllerAdvancedTests()
    {
        var realValidation = new AutomatonValidationService(new TestLogger<AutomatonValidationService>());
        var mockGenerator = new MockAutomatonGeneratorService();
        var mockTempData = new MockAutomatonTempDataService();
        var mockConversion = new MockAutomatonConversionService();
        var mockExecution = new MockAutomatonExecutionService();
        var editingService = new AutomatonEditingService(realValidation, new TestLogger<AutomatonEditingService>());

        controllerWithRealValidation = new AutomatonCreationController(new TestLogger<AutomatonCreationController>(), mockTempData, realValidation, editingService, new MockAutomatonMinimizationService())
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider())
        };

        var mockEditing = new AutomatonEditingService(new MockAutomatonValidationService(), new TestLogger<AutomatonEditingService>());
        controllerWithMocks = new AutomatonCreationController(new TestLogger<AutomatonCreationController>(), mockTempData, new MockAutomatonValidationService(), mockEditing, new MockAutomatonMinimizationService())
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider())
        };
    }

    public static IEnumerable<object[]> EpsilonAcceptedAliases() =>
    [
        ['ε'],
        ['\0']
    ];

    public static IEnumerable<object[]> EpsilonRejectedAliases() =>
    [
        ['ε']
    ];

    [Theory]
    [MemberData(nameof(EpsilonAcceptedAliases))]
    public void AddTransition_EpsilonAliases_AcceptedForEpsilonNFA(string alias)
    {
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.EpsilonNFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true }]
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
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true }]
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
        var controller = new AutomatonCreationController(new TestLogger<AutomatonCreationController>(), new MockAutomatonTempDataService(), realValidation, new AutomatonEditingService(realValidation, new TestLogger<AutomatonEditingService>()), new MockAutomatonMinimizationService())
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
