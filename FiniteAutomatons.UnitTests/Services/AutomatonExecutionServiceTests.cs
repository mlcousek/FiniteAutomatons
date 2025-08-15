using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Services;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Shouldly;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using FiniteAutomatons.Controllers;
using Microsoft.AspNetCore.Mvc;
using FiniteAutomatons.Core.Utilities;
using FiniteAutomatons.UnitTests.Controllers; // for mock services & TestTempDataProvider

namespace FiniteAutomatons.UnitTests.Services;

// Simple no-op test logger
internal class NullLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => false;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
}

public class AutomatonExecutionServiceTests
{
    private readonly IAutomatonBuilderService _builderService = new AutomatonBuilderService(new NullLogger<AutomatonBuilderService>());
    private AutomatonExecutionService CreateService() => new(_builderService, new NullLogger<AutomatonExecutionService>());

    [Fact]
    public void ResetExecution_ClearsState_PreservesStructureAndAlphabet()
    {
        var svc = CreateService();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [ new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true } ],
            Transitions = [ new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }, new() { FromStateId = 2, ToStateId = 1, Symbol = 'b' } ],
            Alphabet = ['a','b'],
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
            Alphabet = [],
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
            Alphabet = ['a'],
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
            Alphabet = [],
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
        // Use controller to exercise logic
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
        vm.States[0].IsStart.ShouldBeTrue(); // reassigned
    }

    public static IEnumerable<object[]> EpsilonAliasCases() => new[]
    {
        new object[]{"?", true},
        new object[]{"epsilon", true},
        new object[]{"a", false}
    };

    [Theory]
    [MemberData(nameof(EpsilonAliasCases))]
    public void Validation_EpsilonAliases_OnlyAllowedInEpsilonNFA(string symbol, bool isEpsilon)
    {
        var validation = new AutomatonValidationService(new NullLogger<AutomatonValidationService>());
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
        var builder = new AutomatonBuilderService(new NullLogger<AutomatonBuilderService>());
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
            Alphabet = ['a'],
            Input = longInput
        };
        svc.ExecuteAll(model);
        model.Position.ShouldBe(longInput.Length);
        model.IsAccepted.ShouldNotBeNull();
        model.IsAccepted!.Value.ShouldBeTrue();
    }

    private AutomatonController BuildControllerWithRealValidation()
    {
        var controller = new AutomatonController(new NullLogger<AutomatonController>(), new MockAutomatonGeneratorService(), new MockAutomatonTempDataService(), new AutomatonValidationService(new NullLogger<AutomatonValidationService>()), new MockAutomatonConversionService(), new MockAutomatonExecutionService())
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider())
        };
        return controller;
    }
}
