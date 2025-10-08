using FiniteAutomatons.Services.Services;
using FiniteAutomatons.Services.Interfaces;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Core.Models.DoMain;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;
using System;
using System.Collections.Generic;
using FiniteAutomatons.UnitTests.TestHelpers;

namespace FiniteAutomatons.UnitTests.Services;

public class AutomatonEditingServiceTests
{
    private readonly AutomatonEditingService _service;
    private readonly IAutomatonValidationService _validation;

    public AutomatonEditingServiceTests()
    {
        _validation = new FiniteAutomatons.Services.Services.AutomatonValidationService(NullLogger<FiniteAutomatons.Services.Services.AutomatonValidationService>.Instance);
        _service = new AutomatonEditingService(_validation, NullLogger<AutomatonEditingService>.Instance);
    }

    [Fact]
    public void AddState_Valid_AddsState()
    {
        var model = new AutomatonViewModel { States = new List<State> { new() { Id = 1, IsStart = true } }, Transitions = new List<Transition>() };
        var (ok, err) = _service.AddState(model, 2, false, true);
        ok.ShouldBeTrue();
        err.ShouldBeNull();
        model.States.Count.ShouldBe(2);
    }

    [Fact]
    public void AddState_DuplicateId_ReturnsError()
    {
        var model = new AutomatonViewModel { States = new List<State> { new() { Id = 1, IsStart = true } }, Transitions = new List<Transition>() };
        var (ok, err) = _service.AddState(model, 1, false, false);
        ok.ShouldBeFalse();
        err.ShouldNotBeNull();
    }

    [Fact]
    public void RemoveState_RemovesTransitionsAndReassignsStart()
    {
        var model = new AutomatonViewModel
        {
            States = new List<State> { new() { Id = 1, IsStart = true }, new() { Id = 2, IsStart = false } },
            Transitions = new List<Transition> { new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' } }
        };

        var (ok, err) = _service.RemoveState(model, 1);
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
            States = new List<State> { new() { Id = 1, IsStart = true }, new() { Id = 2 } },
            Transitions = new List<Transition>()
        };

        var (ok, processed, err) = _service.AddTransition(model, 1, 2, "a");
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
            States = new List<State> { new() { Id = 1, IsStart = true }, new() { Id = 2 } },
            Transitions = new List<Transition>()
        };

        var (ok, err) = _service.RemoveTransition(model, 1, 2, "a");
        ok.ShouldBeFalse();
        err.ShouldNotBeNull();
    }
}
