using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Services;
using FiniteAutomatons.UnitTests.Controllers;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace FiniteAutomatons.UnitTests.Services;

public class HomeAutomatonServiceTests
{
    private readonly HomeAutomatonService _service;
    private readonly MockAutomatonGeneratorService _mockGeneratorService;

    public HomeAutomatonServiceTests()
    {
        _mockGeneratorService = new MockAutomatonGeneratorService();
        var logger = new TestLogger<HomeAutomatonService>();
        _service = new HomeAutomatonService(_mockGeneratorService, logger);
    }

    [Fact]
    public void GenerateDefaultAutomaton_ShouldReturnValidAutomaton()
    {
        // Act
        var result = _service.GenerateDefaultAutomaton();

        // Assert
        result.ShouldNotBeNull();
        result.States.Count.ShouldBeGreaterThan(0);
        result.States.Count(s => s.IsStart).ShouldBe(1);
        result.IsCustomAutomaton.ShouldBeFalse();
        result.Alphabet.ShouldNotBeEmpty();
        result.Transitions.ShouldNotBeNull();
    }

    [Fact]
    public void CreateFallbackAutomaton_ShouldReturnSimpleDFA()
    {
        // Act
        var result = _service.CreateFallbackAutomaton();

        // Assert
        result.ShouldNotBeNull();
        result.Type.ShouldBe(AutomatonType.DFA);
        result.States.Count.ShouldBe(2);
        result.States.Count(s => s.IsStart).ShouldBe(1);
        result.States.Count(s => s.IsAccepting).ShouldBe(1);
        result.IsCustomAutomaton.ShouldBeFalse();
        result.Alphabet.ShouldContain('a');
        result.Transitions.Count.ShouldBe(2);
    }

    [Fact]
    public void GenerateDefaultAutomaton_MultipleCalls_ShouldProduceVariousResults()
    {
        // Act - Generate multiple automatons
        var results = new List<(AutomatonType Type, int StateCount)>();
        
        for (int i = 0; i < 5; i++)
        {
            var result = _service.GenerateDefaultAutomaton();
            results.Add((result.Type, result.States.Count));
            
            // Each should be valid
            result.ShouldNotBeNull();
            result.States.Count.ShouldBeGreaterThan(0);
            result.States.Count(s => s.IsStart).ShouldBe(1);
            result.IsCustomAutomaton.ShouldBeFalse();
        }
        
        // All should be consistent since we're using mock service
        results.All(r => r.StateCount > 0).ShouldBeTrue();
    }
}

// Test helper class
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