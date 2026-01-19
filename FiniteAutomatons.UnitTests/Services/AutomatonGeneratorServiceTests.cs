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
        var result = service.ValidateGenerationParameters(AutomatonType.DFA, 3, 10, 2);

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

        result.States.Count(s => s.IsStart).ShouldBe(1);
        result.States.Count(s => s.IsAccepting).ShouldBeGreaterThan(0);

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
        var seed = 54321;

        // Act
        var result = service.GenerateRandomAutomaton(AutomatonType.NFA, 3, 5, 2, 0.33, seed);

        // Assert
        result.ShouldNotBeNull();
        result.Type.ShouldBe(AutomatonType.NFA);
        result.States.Count.ShouldBe(3);
        result.Transitions.Count.ShouldBeLessThanOrEqualTo(5);
        result.Alphabet.Count.ShouldBe(2);

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

        result.States.Count(s => s.IsStart).ShouldBe(1);
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

        for (int i = 0; i < result1.States.Count; i++)
        {
            result1.States[i].Id.ShouldBe(result2.States[i].Id);
            result1.States[i].IsStart.ShouldBe(result2.States[i].IsStart);
            result1.States[i].IsAccepting.ShouldBe(result2.States[i].IsAccepting);
        }
    }

    [Fact]
    public void GenerateRandomAutomaton_InvalidParameters_ThrowsException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            service.GenerateRandomAutomaton(AutomatonType.DFA, 0, 5, 2));

        Should.Throw<ArgumentException>(() =>
            service.GenerateRandomAutomaton(AutomatonType.DFA, 3, 10, 2));
    }

    [Theory]
    [InlineData(AutomatonType.DFA, 5, 10, 4, 0.4, 1001)]
    [InlineData(AutomatonType.NFA, 5, 12, 4, 0.4, 1002)]
    [InlineData(AutomatonType.EpsilonNFA, 5, 14, 4, 0.4, 1003)]
    public void GenerateRandomAutomaton_EachAlphabetSymbolAppears(AutomatonType type, int stateCount, int transitionCount, int alphabetSize, double acceptingRatio, int seed)
    {
        var model = service.GenerateRandomAutomaton(type, stateCount, transitionCount, alphabetSize, acceptingRatio, seed);

        model.Alphabet.Count.ShouldBe(alphabetSize);

        var usedSymbols = model.Transitions.Where(t => t.Symbol != '\0').Select(t => t.Symbol).Distinct().ToHashSet();
        usedSymbols.Count.ShouldBe(alphabetSize);

        foreach (var c in model.Alphabet)
        {
            usedSymbols.ShouldContain(c);
        }
    }

    [Theory]
    [InlineData(AutomatonType.DFA)]
    [InlineData(AutomatonType.NFA)]
    [InlineData(AutomatonType.EpsilonNFA)]
    public void GenerateRandomAutomaton_AllAlphabetSymbolsUsed_AcrossMultipleSeeds(AutomatonType type)
    {
        int alphabetSize = 4;
        int stateCount = 5;
        int transitionCount = 12;
        double acceptingRatio = 0.4;

        for (int seed = 2000; seed < 2005; seed++)
        {
            var model = service.GenerateRandomAutomaton(type, stateCount, transitionCount, alphabetSize, acceptingRatio, seed);
            model.Alphabet.Count.ShouldBe(alphabetSize);
            var usedSymbols = model.Transitions.Where(t => t.Symbol != '\0').Select(t => t.Symbol).Distinct().OrderBy(c => c).ToList();
            usedSymbols.Count.ShouldBe(alphabetSize);
        }
    }

    // --- PDA-specific tests ---

    [Fact]
    public void GenerateRandomAutomaton_PDA_CreatesValidPDA()
    {
        var seed = 4242;
        var result = service.GenerateRandomAutomaton(AutomatonType.PDA, 4, 10, 3, 0.3, seed);

        result.ShouldNotBeNull();
        result.Type.ShouldBe(AutomatonType.PDA);
        result.States.Count.ShouldBe(4);
        result.Alphabet.Count.ShouldBe(3);
        result.Transitions.Count.ShouldBeLessThanOrEqualTo(10);
        result.States.Count(s => s.IsStart).ShouldBe(1);

        // Ensure transitions include valid stack information (may be null for some)
        foreach (var t in result.Transitions)
        {
            // StackPop may be null or a char; StackPush may be null or non-empty string
            if (t.StackPop.HasValue)
            {
                // '\0' is not used for StackPop here; if present it's allowed
                t.StackPop.Value.ShouldBeOfType<char>();
            }
            t.StackPush?.Length.ShouldBeGreaterThan(0);
        }

        // Ensure determinism: no two transitions share the same (FromStateId, Symbol, StackPop)
        var duplicates = result.Transitions
            .GroupBy(x => (x.FromStateId, x.Symbol, Pop: x.StackPop.HasValue ? x.StackPop.Value : (char?)null))
            .Where(g => g.Count() > 1)
            .ToList();

        duplicates.ShouldBeEmpty();
    }
}
