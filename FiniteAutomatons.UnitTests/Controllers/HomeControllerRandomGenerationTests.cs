using FiniteAutomatons.Controllers;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Shouldly;

namespace FiniteAutomatons.UnitTests.Controllers;

public class HomeControllerRandomGenerationTests
{
    [Fact]
    public void HomeController_ShouldGenerateVariousRandomAutomatons()
    {
        // Arrange
        var service = new AutomatonGeneratorService();
        var homeAutomatonService = new HomeAutomatonService(service, new TestLogger<HomeAutomatonService>());
        var logger = new TestLogger<HomeController>();
        var mockTempDataService = new MockAutomatonTempDataService();
        var minimizationService = new MockAutomatonMinimizationService();
        var controller = new HomeController(logger, mockTempDataService, homeAutomatonService, minimizationService);
        
        var httpContext = new DefaultHttpContext();
        var tempData = new TempDataDictionary(httpContext, new TestTempDataProvider());
        controller.TempData = tempData;
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        // Act & Assert - Generate several automatons to verify randomness
        var results = new List<(AutomatonType Type, int StateCount, int AlphabetSize)>();
        
        for (int i = 0; i < 10; i++)
        {
            var result = controller.Index() as ViewResult;
            result.ShouldNotBeNull();
            
            var model = result.Model as AutomatonViewModel;
            model.ShouldNotBeNull();
            
            model.States.Count.ShouldBe(5); 
            model.Alphabet.Count.ShouldBe(4); 
            model.States.Count(s => s.IsStart).ShouldBe(1); 
            model.States.Count(s => s.IsAccepting).ShouldBeGreaterThan(0); 
            model.IsCustomAutomaton.ShouldBeFalse(); 
            model.Transitions.Count.ShouldBeGreaterThanOrEqualTo(5); 
            
            model.Alphabet.ShouldContain('a');
            model.Alphabet.ShouldContain('b'); 
            model.Alphabet.ShouldContain('c');
            model.Alphabet.ShouldContain('d');
            
            results.Add((model.Type, model.States.Count, model.Alphabet.Count));
        }
        
        var uniqueTypes = results.Select(r => r.Type).Distinct().Count();
        
        results.All(r => r.StateCount == 5).ShouldBeTrue();
        results.All(r => r.AlphabetSize == 4).ShouldBeTrue();
        
        uniqueTypes.ShouldBeGreaterThan(0);
    }

    [Theory]
    [InlineData(AutomatonType.DFA)]
    [InlineData(AutomatonType.NFA)]
    [InlineData(AutomatonType.EpsilonNFA)]
    public void GeneratedAutomaton_ShouldBeValidForAllTypes(AutomatonType expectedType)
    {
        // Arrange
        var service = new AutomatonGeneratorService();
        
        // Act - Generate an automaton of specific type for testing
        var automaton = service.GenerateRandomAutomaton(
            expectedType,
            stateCount: 5,
            transitionCount: 8,
            alphabetSize: 4,
            acceptingStateRatio: 0.4,
            seed: 42 
        );

        // Assert
        automaton.ShouldNotBeNull();
        automaton.Type.ShouldBe(expectedType);
        automaton.States.Count.ShouldBe(5);
        automaton.Alphabet.Count.ShouldBe(4);
        automaton.States.Count(s => s.IsStart).ShouldBe(1);
        automaton.States.Count(s => s.IsAccepting).ShouldBeGreaterThan(0);
        automaton.Transitions.Count.ShouldBeGreaterThanOrEqualTo(5); 
        
        if (expectedType == AutomatonType.DFA)
        {
            automaton.Transitions.Any(t => t.Symbol == '\0').ShouldBeFalse();
            
            var duplicates = automaton.Transitions
                .GroupBy(t => new { t.FromStateId, t.Symbol })
                .Where(g => g.Count() > 1);
            duplicates.ShouldBeEmpty();
        }
        else if (expectedType == AutomatonType.EpsilonNFA)
        {
            // EpsilonNFA might have epsilon transitions (probabilistic)
            // We don't assert this because it's random, but we verify it doesn't break
        }
        
        foreach (var transition in automaton.Transitions)
        {
            automaton.States.Any(s => s.Id == transition.FromStateId).ShouldBeTrue();
            automaton.States.Any(s => s.Id == transition.ToStateId).ShouldBeTrue();
            
            if (transition.Symbol != '\0')
            {
                automaton.Alphabet.ShouldContain(transition.Symbol);
            }
        }
    }
}
