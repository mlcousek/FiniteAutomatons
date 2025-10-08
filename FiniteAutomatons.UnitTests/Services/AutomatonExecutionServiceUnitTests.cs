using FiniteAutomatons.Services.Services;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Core.Models.DoMain;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;
using System;

namespace FiniteAutomatons.UnitTests.Services;

internal class NullLogger2<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => false;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
}

public class AutomatonExecutionServiceUnitTests
{
    private readonly AutomatonExecutionService service;

    public AutomatonExecutionServiceUnitTests()
    {
        var builder = new AutomatonBuilderService(new NullLogger2<AutomatonBuilderService>());
        service = new AutomatonExecutionService(builder, new NullLogger2<AutomatonExecutionService>());
    }

    [Fact]
    public void ExecuteStepForward_AdvancesPosition()
    {
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [ new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true } ],
            Transitions = [ new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' } ],
            Input = "a",
            Position = 0,
            CurrentStateId = 1
        };

        service.ExecuteStepForward(model);
        model.Position.ShouldBe(1);
    }

    [Fact]
    public void ExecuteAll_SetsIsAccepted_ForAcceptingDFA()
    {
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [ new() { Id = 1, IsStart = true, IsAccepting = true } ],
            Transitions = [ new() { FromStateId = 1, ToStateId = 1, Symbol = 'a' } ],
            Input = "aaa"
        };

        service.ExecuteAll(model);
        model.Position.ShouldBe(3);
        model.IsAccepted.ShouldNotBeNull();
        model.IsAccepted!.Value.ShouldBeTrue();
    }
}
