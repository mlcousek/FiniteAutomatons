using FiniteAutomatons.Observability;
using FiniteAutomatons.Services.Observability;
using FiniteAutomatons.Services.Services;
using FiniteAutomatons.Core.Models.ViewModel;
using Shouldly;
using Microsoft.Extensions.Logging;

namespace FiniteAutomatons.UnitTests.Observability;

internal class NullLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => false;
    public void Log<TState>(LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, System.Exception? exception, System.Func<TState, System.Exception?, string> formatter) { }
}

internal class StubBuilderService : AutomatonBuilderService
{
    public StubBuilderService() : base(new NullLogger<AutomatonBuilderService>()) { }
}

public class MoreDecoratorAuditTests
{
    [Fact]
    public void ConversionDecorator_ConvertToDFA_EmitsAudit()
    {
        var audit = new InMemoryAuditService();
        var inner = new AutomatonConversionService(new StubBuilderService(), new NullLogger<AutomatonConversionService>());
        var decorator = new AutomatonConversionServiceAuditorDecorator(inner, audit);

        var model = new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }
            ]
        };

        var converted = decorator.ConvertToDFA(model);

        converted.ShouldNotBeNull();

        var start = audit.GetByEventType("MethodStart").FirstOrDefault(r => r.Message == "IAutomatonConversionService.ConvertToDFA");
        var end = audit.GetByEventType("MethodEnd").FirstOrDefault(r => r.Message == "IAutomatonConversionService.ConvertToDFA");

        start.ShouldNotBeNull();
        end.ShouldNotBeNull();
    }

    [Fact]
    public void ExecutionDecorator_ExecuteStepForward_EmitsAudit()
    {
        var audit = new InMemoryAuditService();
        var inner = new AutomatonExecutionService(new StubBuilderService(), new NullLogger<AutomatonExecutionService>());
        var decorator = new AutomatonExecutionServiceAuditorDecorator(inner, audit);

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
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }
            ],
            Input = "a"
        };

        var updated = decorator.ExecuteStepForward(model);
        updated.ShouldNotBeNull();

        var start = audit.GetByEventType("MethodStart").FirstOrDefault(r => r.Message == "IAutomatonExecutionService.ExecuteStepForward");
        var end = audit.GetByEventType("MethodEnd").FirstOrDefault(r => r.Message == "IAutomatonExecutionService.ExecuteStepForward");

        start.ShouldNotBeNull();
        end.ShouldNotBeNull();
    }
}
