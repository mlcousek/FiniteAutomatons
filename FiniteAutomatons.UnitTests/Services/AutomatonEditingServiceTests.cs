using FiniteAutomatons.Services.Services;
using FiniteAutomatons.Services.Interfaces;
using FiniteAutomatons.Core.Models.ViewModel;
using Shouldly;
using FiniteAutomatons.UnitTests.TestHelpers;

namespace FiniteAutomatons.UnitTests.Services;

public class AutomatonEditingServiceTests
{
    private readonly AutomatonEditingService service;
    private readonly IAutomatonValidationService validation;

    public AutomatonEditingServiceTests()
    {
        validation = new AutomatonValidationService(NullLogger<AutomatonValidationService>.Instance);
        service = new AutomatonEditingService(validation, NullLogger<AutomatonEditingService>.Instance);
    }

    [Fact]
    public void AddState_Valid_AddsState()
    {
        var model = new AutomatonViewModel { States = [new() { Id = 1, IsStart = true }], Transitions = [] };
        var (ok, err) = service.AddState(model, 2, false, true);
        ok.ShouldBeTrue();
        err.ShouldBeNull();
        model.States.Count.ShouldBe(2);
    }

    [Fact]
    public void AddState_DuplicateId_ReturnsError()
    {
        var model = new AutomatonViewModel { States = [new() { Id = 1, IsStart = true }], Transitions = [] };
        var (ok, err) = service.AddState(model, 1, false, false);
        ok.ShouldBeFalse();
        err.ShouldNotBeNull();
    }

    [Fact]
    public void RemoveState_RemovesTransitionsAndReassignsStart()
    {
        var model = new AutomatonViewModel
        {
            States = [new() { Id = 1, IsStart = true }, new() { Id = 2, IsStart = false }],
            Transitions = [new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }]
        };

        var (ok, err) = service.RemoveState(model, 1);
        ok.ShouldBeTrue();
        err.ShouldBeNull();
        model.States.Count.ShouldBe(1);
        model.Transitions.Count.ShouldBe(0);
        model.States[0].IsStart.ShouldBeTrue();
    }

    [Fact]
    public void AddTransition_Valid_AddsTransition()
    {
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States = [new() { Id = 1, IsStart = true }, new() { Id = 2 }],
            Transitions = []
        };

        var (ok, processed, err) = service.AddTransition(model, 1, 2, "a");
        ok.ShouldBeTrue();
        err.ShouldBeNull();
        processed.ShouldBe('a');
        model.Transitions.Count.ShouldBe(1);
    }

    [Fact]
    public void RemoveTransition_NotFound_ReturnsError()
    {
        var model = new AutomatonViewModel
        {
            States = [new() { Id = 1, IsStart = true }, new() { Id = 2 }],
            Transitions = []
        };

        var (ok, err) = service.RemoveTransition(model, 1, 2, "a");
        ok.ShouldBeFalse();
        err.ShouldNotBeNull();
    }
}
