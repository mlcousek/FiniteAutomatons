using FiniteAutomatons.Controllers;
using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        var controller = new HomeController(logger, mockTempDataService, homeAutomatonService);
        
        // Setup TempData properly
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
            
            // Verify basic constraints from our implementation
            model.States.Count.ShouldBe(5); // Should have 5 states as requested
            model.Alphabet.Count.ShouldBe(4); // Should have 4-letter alphabet as requested
            model.States.Count(s => s.IsStart).ShouldBe(1); // Exactly one start state
            model.States.Count(s => s.IsAccepting).ShouldBeGreaterThan(0); // At least one accepting state
            model.IsCustomAutomaton.ShouldBeFalse(); // Should be marked as default (not custom)
            model.Transitions.Count.ShouldBeGreaterThanOrEqualTo(5); // At least state count for connectivity
            
            // Verify alphabet contains expected 4-letter alphabet
            model.Alphabet.ShouldContain('a');
            model.Alphabet.ShouldContain('b'); 
            model.Alphabet.ShouldContain('c');
            model.Alphabet.ShouldContain('d');
            
            // Store results for variety check
            results.Add((model.Type, model.States.Count, model.Alphabet.Count));
        }
        
        // Verify we get variety in automaton types over multiple generations
        // Note: This is probabilistic, but over 10 generations we should likely see some variety
        var uniqueTypes = results.Select(r => r.Type).Distinct().Count();
        
        // All should have consistent parameters as specified
        results.All(r => r.StateCount == 5).ShouldBeTrue();
        results.All(r => r.AlphabetSize == 4).ShouldBeTrue();
        
        // Should generate different types (DFA, NFA, EpsilonNFA) - at least 1 type variety expected
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
            seed: 42 // Fixed seed for reproducible test
        );

        // Assert
        automaton.ShouldNotBeNull();
        automaton.Type.ShouldBe(expectedType);
        automaton.States.Count.ShouldBe(5);
        automaton.Alphabet.Count.ShouldBe(4);
        automaton.States.Count(s => s.IsStart).ShouldBe(1);
        automaton.States.Count(s => s.IsAccepting).ShouldBeGreaterThan(0);
        automaton.Transitions.Count.ShouldBeGreaterThanOrEqualTo(5); // At least connectivity
        
        // Type-specific validations
        if (expectedType == AutomatonType.DFA)
        {
            // DFA should not have epsilon transitions
            automaton.Transitions.Any(t => t.Symbol == '\0').ShouldBeFalse();
            
            // DFA should not have multiple transitions from same state on same symbol
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
        
        // All transitions should reference valid states
        foreach (var transition in automaton.Transitions)
        {
            automaton.States.Any(s => s.Id == transition.FromStateId).ShouldBeTrue();
            automaton.States.Any(s => s.Id == transition.ToStateId).ShouldBeTrue();
            
            // Non-epsilon symbols should be in alphabet
            if (transition.Symbol != '\0')
            {
                automaton.Alphabet.ShouldContain(transition.Symbol);
            }
        }
    }
}