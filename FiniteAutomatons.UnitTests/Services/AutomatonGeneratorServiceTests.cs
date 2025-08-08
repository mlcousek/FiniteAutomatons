using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Services;
using Shouldly;

namespace FiniteAutomatons.UnitTests.Services;

public class AutomatonGeneratorServiceTests
{
    private readonly AutomatonGeneratorService service;

    public AutomatonGeneratorServiceTests()
    {
        service = new AutomatonGeneratorService();
    }

    [Fact]
    public void ValidateGenerationParameters_ValidDFA_ReturnsTrue()
    {
        // Act
        var result = service.ValidateGenerationParameters(AutomatonType.DFA, 3, 6, 2);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ValidateGenerationParameters_TooManyTransitionsForDFA_ReturnsFalse()
    {
        // Act
        var result = service.ValidateGenerationParameters(AutomatonType.DFA, 3, 10, 2); // Max would be 3*2=6

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ValidateGenerationParameters_InvalidStateCount_ReturnsFalse()
    {
        // Act
        var result = service.ValidateGenerationParameters(AutomatonType.DFA, 0, 5, 2);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void GenerateRandomAutomaton_DFA_CreatesValidAutomaton()
    {
        // Arrange
        var seed = 12345; // Fixed seed for reproducible test

        // Act
        var result = service.GenerateRandomAutomaton(AutomatonType.DFA, 4, 6, 2, 0.5, seed);

        // Assert
        result.ShouldNotBeNull();
        result.Type.ShouldBe(AutomatonType.DFA);
        result.States.Count.ShouldBe(4);
        result.Transitions.Count.ShouldBeLessThanOrEqualTo(6);
        result.Alphabet.Count.ShouldBe(2);
        result.IsCustomAutomaton.ShouldBeTrue();

        // Should have exactly one start state
        result.States.Count(s => s.IsStart).ShouldBe(1);

        // Should have at least one accepting state
        result.States.Count(s => s.IsAccepting).ShouldBeGreaterThan(0);

        // All transition symbols should be in alphabet
        foreach (var transition in result.Transitions)
        {
            if (transition.Symbol != '\0') // Not epsilon
            {
                result.Alphabet.ShouldContain(transition.Symbol);
            }
        }
    }

    [Fact]
    public void GenerateRandomAutomaton_NFA_CreatesValidAutomaton()
    {
        // Arrange
        var seed = 54321; // Fixed seed for reproducible test

        // Act
        var result = service.GenerateRandomAutomaton(AutomatonType.NFA, 3, 5, 2, 0.33, seed);

        // Assert
        result.ShouldNotBeNull();
        result.Type.ShouldBe(AutomatonType.NFA);
        result.States.Count.ShouldBe(3);
        result.Transitions.Count.ShouldBeLessThanOrEqualTo(5);
        result.Alphabet.Count.ShouldBe(2);

        // Should have exactly one start state
        result.States.Count(s => s.IsStart).ShouldBe(1);
    }

    [Fact]
    public void GenerateRandomAutomaton_EpsilonNFA_CreatesValidAutomaton()
    {
        // Arrange
        var seed = 99999; // Fixed seed for reproducible test

        // Act
        var result = service.GenerateRandomAutomaton(AutomatonType.EpsilonNFA, 4, 8, 3, 0.25, seed);

        // Assert
        result.ShouldNotBeNull();
        result.Type.ShouldBe(AutomatonType.EpsilonNFA);
        result.States.Count.ShouldBe(4);
        result.Alphabet.Count.ShouldBe(3);

        // Should have exactly one start state
        result.States.Count(s => s.IsStart).ShouldBe(1);

        // May have epsilon transitions (symbol = '\0')
        var hasEpsilonTransitions = result.Transitions.Any(t => t.Symbol == '\0');
        // Note: This is probabilistic, so we just check it doesn't crash
    }

    [Fact]
    public void GenerateRandomAutomaton_WithSeed_IsReproducible()
    {
        // Arrange
        var seed = 42;

        // Act
        var result1 = service.GenerateRandomAutomaton(AutomatonType.DFA, 3, 4, 2, 0.33, seed);
        var result2 = service.GenerateRandomAutomaton(AutomatonType.DFA, 3, 4, 2, 0.33, seed);

        // Assert
        result1.States.Count.ShouldBe(result2.States.Count);
        result1.Transitions.Count.ShouldBe(result2.Transitions.Count);
        result1.Alphabet.Count.ShouldBe(result2.Alphabet.Count);

        // States should be identical
        for (int i = 0; i < result1.States.Count; i++)
        {
            result1.States[i].Id.ShouldBe(result2.States[i].Id);
            result1.States[i].IsStart.ShouldBe(result2.States[i].IsStart);
            result1.States[i].IsAccepting.ShouldBe(result2.States[i].IsAccepting);
        }
    }

    [Fact]
    public void GenerateRealisticAutomaton_DFA_CreatesReasonableAutomaton()
    {
        // Act
        var result = service.GenerateRealisticAutomaton(AutomatonType.DFA, 5, 123);

        // Assert
        result.ShouldNotBeNull();
        result.Type.ShouldBe(AutomatonType.DFA);
        result.States.Count.ShouldBe(5);
        result.Transitions.Count.ShouldBeGreaterThanOrEqualTo(5); // At least state count for connectivity
        result.Alphabet.Count.ShouldBeInRange(3, 6); // Realistic alphabet size

        // Should have exactly one start state
        result.States.Count(s => s.IsStart).ShouldBe(1);

        // Should have reasonable number of accepting states
        var acceptingCount = result.States.Count(s => s.IsAccepting);
        acceptingCount.ShouldBeInRange(1, 4); // 20-60% of 5 states
    }

    [Fact]
    public void GenerateRandomAutomaton_InvalidParameters_ThrowsException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => 
            service.GenerateRandomAutomaton(AutomatonType.DFA, 0, 5, 2));

        Should.Throw<ArgumentException>(() => 
            service.GenerateRandomAutomaton(AutomatonType.DFA, 3, 10, 2)); // Too many transitions for DFA
    }

    [Fact]
    public void GenerateRealisticAutomaton_InvalidStateCount_ThrowsException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => 
            service.GenerateRealisticAutomaton(AutomatonType.DFA, 0));
    }

    [Theory]
    [InlineData(AutomatonType.DFA)]
    [InlineData(AutomatonType.NFA)]
    [InlineData(AutomatonType.EpsilonNFA)]
    public void GenerateRandomAutomaton_AllTypes_ProduceValidAutomatons(AutomatonType type)
    {
        // Arrange
        var seed = 777;

        // Act
        var result = service.GenerateRandomAutomaton(type, 4, 8, 3, 0.5, seed);

        // Assert
        result.ShouldNotBeNull();
        result.Type.ShouldBe(type);
        result.States.Count.ShouldBe(4);
        result.States.Count(s => s.IsStart).ShouldBe(1);
        result.Transitions.ShouldNotBeEmpty();
        result.Alphabet.ShouldNotBeEmpty();
        result.IsCustomAutomaton.ShouldBeTrue();

        // All states should have valid IDs
        result.States.All(s => s.Id > 0).ShouldBeTrue();

        // All transitions should reference valid states
        foreach (var transition in result.Transitions)
        {
            result.States.Any(s => s.Id == transition.FromStateId).ShouldBeTrue();
            result.States.Any(s => s.Id == transition.ToStateId).ShouldBeTrue();
        }
    }
}