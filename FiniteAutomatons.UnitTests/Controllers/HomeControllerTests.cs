using FiniteAutomatons.Controllers;
using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace FiniteAutomatons.UnitTests.Controllers;

public class HomeControllerTests
{
    private readonly HomeController controller;

    public HomeControllerTests()
    {
        var logger = new TestLogger<HomeController>();
        var mockTempDataService = new MockAutomatonTempDataService();
        var mockHomeAutomatonService = new MockHomeAutomatonService();
        controller = new HomeController(logger, mockTempDataService, mockHomeAutomatonService);

        // Setup HttpContext and TempData
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "test-trace-id"
        };
        var tempData = new TempDataDictionary(httpContext, new TestTempDataProvider());
        controller.TempData = tempData;
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public void Index_WithoutCustomAutomaton_ReturnsRandomlyGeneratedAutomaton()
    {
        // Act
        var result = controller.Index() as ViewResult;

        // Assert
        result.ShouldNotBeNull();
        var model = result.Model as AutomatonViewModel;
        model.ShouldNotBeNull();
        
        // Should return a generated automaton (using mock service)
        model.States.Count.ShouldBeGreaterThan(0);
        model.States.Count(s => s.IsStart).ShouldBe(1);
        model.IsCustomAutomaton.ShouldBeFalse(); // Should be marked as default (non-custom)
        model.Alphabet.ShouldNotBeEmpty();
        model.Transitions.ShouldNotBeNull();
    }

    [Fact]
    public void Privacy_ReturnsView()
    {
        // Act
        var result = controller.Privacy() as ViewResult;

        // Assert
        result.ShouldNotBeNull();
    }

    [Fact]
    public void Error_ReturnsErrorView()
    {
        // Act
        var result = controller.Error() as ViewResult;

        // Assert
        result.ShouldNotBeNull();
        var model = result.Model as ErrorViewModel;
        model.ShouldNotBeNull();
        model.RequestId.ShouldBe("test-trace-id");
        model.ShowRequestId.ShouldBeTrue();
    }
}

// Test helper classes
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

public class TestTempDataProvider : ITempDataProvider
{
    private readonly Dictionary<string, object?> _data = [];

    public IDictionary<string, object?> LoadTempData(HttpContext context)
    {
        return _data;
    }

    public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
    {
        _data.Clear();
        foreach (var kvp in values)
        {
            _data[kvp.Key] = kvp.Value;
        }
    }
}
