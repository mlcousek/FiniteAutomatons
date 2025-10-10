using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Services;
using FiniteAutomatons.Services.Interfaces;
using Shouldly;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using FiniteAutomatons.Controllers;
using Microsoft.AspNetCore.Mvc;
using FiniteAutomatons.Core.Utilities;
using FiniteAutomatons.UnitTests.Controllers; 
using FiniteAutomatons.UnitTests.TestHelpers;

namespace FiniteAutomatons.UnitTests.Services;

public class AutomatonExecutionServiceTests
{
    private readonly IAutomatonBuilderService builderService = new AutomatonBuilderService(NullLogger<AutomatonBuilderService>.Instance);
    private AutomatonExecutionService CreateService() => new(builderService, NullLogger<AutomatonExecutionService>.Instance);

    [Fact]
    public void ResetExecution_ClearsState_PreservesStructureAndAlphabet()
    {
        var svc = CreateService();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [ new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true } ],
            Transitions = [ new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }, new() { FromStateId = 2, ToStateId = 1, Symbol = 'b' } ],
            Input = "abba",
            Position = 2,
            CurrentStateId = 2,
            Result = true,
            IsAccepted = true,
            StateHistorySerialized = "[[1],[2]]"
        };

        svc.ResetExecution(model);

        model.States.Count.ShouldBe(2);
        model.Transitions.Count.ShouldBe(2);
        model.Alphabet.ShouldBe(['a','b']);
        model.Input.ShouldBe("");
        model.Position.ShouldBe(0);
        model.CurrentStateId.ShouldBeNull();
        model.Result.ShouldBeNull();
        model.IsAccepted.ShouldBeNull();
        model.StateHistorySerialized.ShouldBe("");
    }

    [Fact]
    public void StepForward_DoesNotAdvancePastEnd()
    {
        var svc = CreateService();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [ new() { Id = 1, IsStart = true, IsAccepting = true } ],
            Transitions = [],
            Input = "a",
            Position = 1, // already at end
            CurrentStateId = 1
        };

        svc.ExecuteStepForward(model);
        model.Position.ShouldBe(1); // unchanged
    }

    [Fact]
    public void BackToStart_ReinitializesDFAState()
    {
        var svc = CreateService();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [ new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true } ],
            Transitions = [ new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' } ],
            Input = "a",
            Position = 1,
            CurrentStateId = 2,
            IsAccepted = true
        };

        svc.BackToStart(model);
        model.Position.ShouldBe(0);
        model.CurrentStateId.ShouldBe(1);
        model.IsAccepted.ShouldBeNull();
    }

    [Fact]
    public void ReconstructState_DeserializesHistory_DFA()
    {
        var svc = CreateService();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [ new() { Id = 1, IsStart = true, IsAccepting = false } ],
            Transitions = [],
            Input = "ab",
            Position = 1,
            CurrentStateId = 1,
            StateHistorySerialized = "[[1]]"
        };

        var state = svc.ReconstructState(model);
        state.StateHistory.Count.ShouldBe(1);
        state.StateHistory.Peek().ShouldContain(1);
    }

    [Fact]
    public void RemoveStartState_AutoAssignsNewStart_InController()
    {
        var controller = BuildControllerWithRealValidation();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [ new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true } ]
        };

        var result = controller.RemoveState(model, 1) as ViewResult;
        var vm = result!.Model as AutomatonViewModel;
        vm!.States.Count.ShouldBe(1);
        vm.States[0].Id.ShouldBe(2);
        vm.States[0].IsStart.ShouldBeTrue(); 
    }

    public static IEnumerable<object[]> EpsilonAliasCases() =>
    [
        ["?", true],
        ["epsilon", true],
        ["a", false]
    ];

    [Theory]
    [MemberData(nameof(EpsilonAliasCases))]
    public void Validation_EpsilonAliases_OnlyAllowedInEpsilonNFA(string symbol, bool isEpsilon)
    {
        var validation = new AutomatonValidationService(NullLogger<AutomatonValidationService>.Instance);
        var epsilonModel = new AutomatonViewModel { Type = AutomatonType.EpsilonNFA, States = [ new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true } ] };
        var dfaModel = new AutomatonViewModel { Type = AutomatonType.DFA, States = [ new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true } ] };

        var (okEpsilon, procEpsilon, _) = validation.ValidateTransitionAddition(epsilonModel, 1, 2, symbol);
        var (okDfa, _, errorDfa) = validation.ValidateTransitionAddition(dfaModel, 1, 2, symbol);

        if (isEpsilon)
        {
            okEpsilon.ShouldBeTrue();
            procEpsilon.ShouldBe(AutomatonSymbolHelper.EpsilonInternal);
            okDfa.ShouldBeFalse();
            errorDfa.ShouldNotBeNull();
        }
        else
        {
            okEpsilon.ShouldBeTrue();
            okDfa.ShouldBeTrue();
        }
    }

    [Fact]
    public void Builder_AddingSecondStartState_ShouldThrow()
    {
        var builder = new AutomatonBuilderService(NullLogger<AutomatonBuilderService>.Instance);
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [ new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = true, IsAccepting = false } ] // invalid
        };
        Should.Throw<InvalidOperationException>(() => builder.CreateAutomatonFromModel(model));
    }

    [Fact]
    public void ExecuteAll_LargeInput_NoCrash()
    {
        var svc = CreateService();
        var longInput = new string('a', 2000);
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [ new() { Id = 1, IsStart = true, IsAccepting = true } ],
            Transitions = [ new() { FromStateId = 1, ToStateId = 1, Symbol = 'a' } ],
            Input = longInput
        };
        svc.ExecuteAll(model);
        model.Position.ShouldBe(longInput.Length);
        model.IsAccepted.ShouldNotBeNull();
        model.IsAccepted!.Value.ShouldBeTrue();
    }

    private AutomatonController BuildControllerWithRealValidation()
    {
        var controller = new AutomatonController(NullLogger<AutomatonController>.Instance, new MockAutomatonGeneratorService(), new MockAutomatonTempDataService(), new AutomatonValidationService(NullLogger<AutomatonValidationService>.Instance), new MockAutomatonConversionService(), new MockAutomatonExecutionService(), new AutomatonEditingService(new AutomatonValidationService(NullLogger<AutomatonValidationService>.Instance), NullLogger<AutomatonEditingService>.Instance), new MockAutomatonFileService())
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider())
        };
        return controller;
    }
}
