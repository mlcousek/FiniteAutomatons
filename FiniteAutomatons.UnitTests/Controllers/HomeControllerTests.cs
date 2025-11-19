using FiniteAutomatons.Controllers;
using FiniteAutomatons.Core.Models.ViewModel;
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
        var minimizationService = new MockAutomatonMinimizationService();
        controller = new HomeController(logger, mockTempDataService, mockHomeAutomatonService, minimizationService);

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
        
        model.States.Count.ShouldBeGreaterThan(0);
        model.States.Count(s => s.IsStart).ShouldBe(1);
        model.IsCustomAutomaton.ShouldBeFalse(); 
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
    private readonly Dictionary<string, object?> data = [];

    public IDictionary<string, object?> LoadTempData(HttpContext context)
    {
        return data;
    }

    public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
    {
        data.Clear();
        foreach (var kvp in values)
        {
            data[kvp.Key] = kvp.Value;
        }
    }
}
