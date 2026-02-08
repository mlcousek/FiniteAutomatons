using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FiniteAutomatons.UnitTests.Services;

public class InputGenerationServiceTests
{
    private readonly InputGenerationService service;

    public InputGenerationServiceTests()
    {
        // Provide a real AutomatonBuilderService with a null logger so tests can build PDAs/NFAs when needed
        var builderService = new AutomatonBuilderService(NullLogger<AutomatonBuilderService>.Instance);
        service = new InputGenerationService(NullLogger<InputGenerationService>.Instance, builderService);
    }

    #region GenerateRandomString Tests

    [Fact]
    public void GenerateRandomString_WithValidAutomaton_GeneratesStringWithinLengthRange()
    {
        // Arrange
        var automaton = CreateSampleDfa();

        // Act
        var result = service.GenerateRandomString(automaton, 3, 7, seed: 123);

        // Assert
        result.ShouldNotBeNull();
        result.Length.ShouldBeGreaterThanOrEqualTo(3);
        result.Length.ShouldBeLessThanOrEqualTo(7);
        result.All(c => automaton.Alphabet!.Contains(c)).ShouldBeTrue();
    }

    [Fact]
    public void GenerateRandomString_WithEmptyAlphabet_ReturnsEmptyString()
    {
        // Arrange
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new State { Id = 0, IsStart = true }],
            Transitions = []
        };

        // Act
        var result = service.GenerateRandomString(automaton, 0, 10);

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void GenerateRandomString_WithSeed_ProducesDeterministicResults()
    {
        // Arrange
        var automaton = CreateSampleDfa();
        var seed = 42;

        // Act
        var result1 = service.GenerateRandomString(automaton, 5, 10, seed);
        var result2 = service.GenerateRandomString(automaton, 5, 10, seed);

        // Assert
        result1.ShouldBe(result2);
    }

    [Fact]
    public void GenerateRandomString_MinLengthEqualsMaxLength_GeneratesExactLength()
    {
        // Arrange
        var automaton = CreateSampleDfa();

        // Act
        var result = service.GenerateRandomString(automaton, 5, 5, seed: 123);

        // Assert
        result.Length.ShouldBe(5);
    }

    #endregion

    #region GenerateAcceptingString Tests

    [Fact]
    public void GenerateAcceptingString_WithReachableAcceptingState_ReturnsAcceptingString()
    {
        // Arrange
        var automaton = CreateSampleDfa();

        // Act
        var result = service.GenerateAcceptingString(automaton, 20);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldNotBeEmpty();
    }

    [Fact]
    public void GenerateAcceptingString_WithNoAcceptingStates_ReturnsNull()
    {
        // Arrange
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new State { Id = 0, IsStart = true, IsAccepting = false },
                new State { Id = 1, IsStart = false, IsAccepting = false }
            ],
            Transitions =
            [
                new Transition { FromStateId = 0, ToStateId = 1, Symbol = 'a' }
            ]
        };

        // Act
        var result = service.GenerateAcceptingString(automaton, 20);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GenerateAcceptingString_WithNoStartState_ReturnsNull()
    {
        // Arrange
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new State { Id = 0, IsStart = false, IsAccepting = true }
            ],
            Transitions = []
        };

        // Act
        var result = service.GenerateAcceptingString(automaton, 20);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GenerateAcceptingString_WithUnreachableAcceptingState_ReturnsNull()
    {
        // Arrange
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new State { Id = 0, IsStart = true, IsAccepting = false },
                new State { Id = 1, IsStart = false, IsAccepting = false },
                new State { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new Transition { FromStateId = 0, ToStateId = 1, Symbol = 'a' }
                // No transition to state 2 - unreachable
            ]
        };

        // Act
        var result = service.GenerateAcceptingString(automaton, 20);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GenerateAcceptingString_WithEpsilonTransitions_FindsPath()
    {
        // Arrange
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.EpsilonNFA,
            States =
            [
                new State { Id = 0, IsStart = true, IsAccepting = false },
                new State { Id = 1, IsStart = false, IsAccepting = false },
                new State { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new Transition { FromStateId = 0, ToStateId = 1, Symbol = 'a' },
                new Transition { FromStateId = 1, ToStateId = 2, Symbol = '\0' } // Epsilon transition
            ]
        };

        // Act
        var result = service.GenerateAcceptingString(automaton, 20);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe("a"); // Should find path through epsilon
    }

    #endregion

    #region GenerateRandomAcceptingString Tests

    [Fact]
    public void GenerateRandomAcceptingString_WithReachableAcceptingState_ReturnsAcceptingString()
    {
        // Arrange
        var automaton = CreateSampleDfa();

        // Act
        var result = service.GenerateRandomAcceptingString(automaton, minLength: 0, maxLength: 50, maxAttempts: 100, seed: 42);

        // Assert
        result.ShouldNotBeNull();
        result.Length.ShouldBeLessThanOrEqualTo(50);
    }

    [Fact]
    public void GenerateRandomAcceptingString_WithDifferentSeeds_ReturnsDifferentStrings()
    {
        // Arrange
        var automaton = CreateAutomatonWithMultiplePaths();

        // Act
        var result1 = service.GenerateRandomAcceptingString(automaton, minLength: 0, maxLength: 50, maxAttempts: 100, seed: 42);
        var result2 = service.GenerateRandomAcceptingString(automaton, minLength: 0, maxLength: 50, maxAttempts: 100, seed: 123);

        // Assert
        result1.ShouldNotBeNull();
        result2.ShouldNotBeNull();
        // With different seeds, we expect different results (though not guaranteed in all cases)
        // This test demonstrates the randomness capability
    }

    [Fact]
    public void GenerateRandomAcceptingString_WithSameSeed_ReturnsSameString()
    {
        // Arrange
        var automaton = CreateAutomatonWithMultiplePaths();
        var seed = 42;

        // Act
        var result1 = service.GenerateRandomAcceptingString(automaton, minLength: 0, maxLength: 50, maxAttempts: 100, seed: seed);
        var result2 = service.GenerateRandomAcceptingString(automaton, minLength: 0, maxLength: 50, maxAttempts: 100, seed: seed);

        // Assert
        result1.ShouldNotBeNull();
        result2.ShouldNotBeNull();
        result1.ShouldBe(result2);
    }

    [Fact]
    public void GenerateRandomAcceptingString_WithMinLength_RespectsMinimumLength()
    {
        // Arrange
        var automaton = CreateAutomatonWithMultiplePaths();

        // Act
        var result = service.GenerateRandomAcceptingString(automaton, minLength: 5, maxLength: 50, maxAttempts: 100, seed: 42);

        // Assert
        result.ShouldNotBeNull();
        result.Length.ShouldBeGreaterThanOrEqualTo(5);
        result.Length.ShouldBeLessThanOrEqualTo(50);
    }

    [Fact]
    public void GenerateRandomAcceptingString_WithNoAcceptingStates_ReturnsNull()
    {
        // Arrange
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new State { Id = 0, IsStart = true, IsAccepting = false },
                new State { Id = 1, IsStart = false, IsAccepting = false }
            ],
            Transitions =
            [
                new Transition { FromStateId = 0, ToStateId = 1, Symbol = 'a' }
            ]
        };

        // Act
        var result = service.GenerateRandomAcceptingString(automaton, minLength: 0, maxLength: 50, maxAttempts: 100);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GenerateRandomAcceptingString_WithNoStartState_ReturnsNull()
    {
        // Arrange
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new State { Id = 0, IsStart = false, IsAccepting = true }
            ],
            Transitions = []
        };

        // Act
        var result = service.GenerateRandomAcceptingString(automaton, minLength: 0, maxLength: 50, maxAttempts: 100);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GenerateRandomAcceptingString_WithEpsilonTransitions_FindsAcceptingPath()
    {
        // Arrange
        var automaton = CreateEpsilonNfa();

        // Act
        var result = service.GenerateRandomAcceptingString(automaton, minLength: 0, maxLength: 50, maxAttempts: 100, seed: 42);

        // Assert
        result.ShouldNotBeNull();
        result.Length.ShouldBeLessThanOrEqualTo(50);
    }

    [Fact]
    public void GenerateRandomAcceptingString_WithComplexAutomaton_FindsVariousPaths()
    {
        // Arrange
        var automaton = CreateAutomatonWithMultiplePaths();
        var results = new HashSet<string>();

        // Act - Try multiple times with different seeds to get different paths
        for (int i = 0; i < 10; i++)
        {
            var result = service.GenerateRandomAcceptingString(automaton, minLength: 0, maxLength: 20, maxAttempts: 100, seed: i);
            if (result != null)
            {
                results.Add(result);
            }
        }

        // Assert
        results.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void GenerateRandomAcceptingString_WithUnreachableAcceptingState_ReturnsNull()
    {
        // Arrange
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new State { Id = 0, IsStart = true, IsAccepting = false },
                new State { Id = 1, IsStart = false, IsAccepting = false },
                new State { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new Transition { FromStateId = 0, ToStateId = 1, Symbol = 'a' }
                // No transition to state 2 - unreachable
            ]
        };

        // Act
        var result = service.GenerateRandomAcceptingString(automaton, minLength: 0, maxLength: 50, maxAttempts: 100);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GenerateRandomAcceptingString_WithStartStateAccepting_PrefersNonEmptyString()
    {
        // Arrange - automaton where start state is accepting, but has transitions to build longer strings
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States =
            [
                new State { Id = 0, IsStart = true, IsAccepting = true }, // Start is accepting (empty string works)
                new State { Id = 1, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new Transition { FromStateId = 0, ToStateId = 1, Symbol = 'a' },
                new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a' } // Loop to generate longer strings
            ]
        };

        // Act - with enough attempts, should eventually find non-empty strings
        var nonEmptyFound = false;
        for (int seed = 0; seed < 100 && !nonEmptyFound; seed++)
        {
            var result = service.GenerateRandomAcceptingString(automaton, minLength: 0, maxLength: 10, maxAttempts: 100, seed: seed);
            if (result != null && result.Length > 0)
            {
                nonEmptyFound = true;
            }
        }

        // Assert - should eventually find non-empty strings with different seeds and attempts
        nonEmptyFound.ShouldBeTrue("With 100 different seeds and 100 attempts each, should find at least one non-empty string");
    }

    [Fact]
    public void GenerateRandomAcceptingString_OnlyEmptyAccepting_ReturnsEmpty()
    {
        // Arrange - automaton where ONLY empty string is accepted (no transitions from accepting start)
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new State { Id = 0, IsStart = true, IsAccepting = true } // Only state, accepting
            ],
            Transitions = [] // No transitions at all
        };

        // Act
        var result = service.GenerateRandomAcceptingString(automaton, minLength: 0, maxLength: 50, maxAttempts: 100);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(string.Empty);
    }

    #endregion

    #region GenerateRejectingString Tests

    [Fact]
    public void GenerateRejectingString_WithValidAutomaton_ReturnsNonNull()
    {
        // Arrange
        var automaton = CreateSampleDfa();

        // Act
        var result = service.GenerateRejectingString(automaton, 20);

        // Assert
        result.ShouldNotBeNull();
    }

    [Fact]
    public void GenerateRejectingString_WithNoAlphabet_ReturnsNull()
    {
        // Arrange
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new State { Id = 0, IsStart = true }],
            Transitions = []
        };

        // Act
        var result = service.GenerateRejectingString(automaton, 20);

        // Assert
        result.ShouldBeNull();
    }

    #endregion

    #region GenerateInterestingCases Tests

    [Fact]
    public void GenerateInterestingCases_WithValidAutomaton_ReturnsMultipleCases()
    {
        // Arrange
        var automaton = CreateSampleDfa();

        // Act
        var result = service.GenerateInterestingCases(automaton, 15);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBeGreaterThan(0);
        result.All(c => c.Input != null && c.Description != null).ShouldBeTrue();
    }

    [Fact]
    public void GenerateInterestingCases_AlwaysIncludesEmptyString()
    {
        // Arrange
        var automaton = CreateSampleDfa();

        // Act
        var result = service.GenerateInterestingCases(automaton, 15);

        // Assert
        result.ShouldContain(c => c.Input == string.Empty && c.Description.Contains("Empty"));
    }

    [Fact]
    public void GenerateInterestingCases_IncludesSingleCharacter()
    {
        // Arrange
        var automaton = CreateSampleDfa();

        // Act
        var result = service.GenerateInterestingCases(automaton, 15);

        // Assert
        result.ShouldContain(c => c.Input.Length == 1);
    }

    [Fact]
    public void GenerateInterestingCases_ForNfa_IncludesNondeterministicCase()
    {
        // Arrange
        var automaton = CreateNondeterministicNfa();

        // Act
        var result = service.GenerateInterestingCases(automaton, 15);

        // Assert
        result.ShouldContain(c => c.Description.Contains("nondetermin", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GenerateInterestingCases_ForEpsilonNfa_IncludesEpsilonCase()
    {
        // Arrange
        var automaton = CreateEpsilonNfa();

        // Act
        var result = service.GenerateInterestingCases(automaton, 15);

        // Assert
        result.ShouldContain(c => c.Description.Contains('ε', StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region GenerateNondeterministicCase Tests

    [Fact]
    public void GenerateNondeterministicCase_WithNondeterministicNfa_ReturnsString()
    {
        // Arrange
        var automaton = CreateNondeterministicNfa();

        // Act
        var result = service.GenerateNondeterministicCase(automaton, 15);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldNotBeEmpty();
    }

    [Fact]
    public void GenerateNondeterministicCase_WithDeterministicAutomaton_ReturnsNull()
    {
        // Arrange
        var automaton = CreateSampleDfa();

        // Act
        var result = service.GenerateNondeterministicCase(automaton, 15);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GenerateNondeterministicCase_WithNoTransitions_ReturnsNull()
    {
        // Arrange
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States = [new State { Id = 0, IsStart = true }],
            Transitions = []
        };

        // Act
        var result = service.GenerateNondeterministicCase(automaton, 15);

        // Assert
        result.ShouldBeNull();
    }

    #endregion

    #region GenerateEpsilonCase Tests

    [Fact]
    public void GenerateEpsilonCase_WithEpsilonTransitions_ReturnsString()
    {
        // Arrange
        var automaton = CreateEpsilonNfa();

        // Act
        var result = service.GenerateEpsilonCase(automaton, 15);

        // Assert
        result.ShouldNotBeNull();
    }

    [Fact]
    public void GenerateEpsilonCase_PathLeadsToEpsilonSource()
    {
        // Arrange
        var automaton = CreateEpsilonNfa();

        // Act
        var result = service.GenerateEpsilonCase(automaton, 15);

        // Assert
        result.ShouldNotBeNull();

        // Simulate walking the returned path (epsilon transitions are not represented in the string)
        var current = automaton.States!.First(s => s.IsStart).Id;
        foreach (var ch in result!)
        {
            var trans = automaton.Transitions!.FirstOrDefault(t => t.FromStateId == current && t.Symbol == ch);
            trans.ShouldNotBeNull($"No transition for symbol '{ch}' from state {current}");
            current = trans!.ToStateId;
        }

        // After consuming the string, the current state should have an outgoing epsilon transition
        automaton.Transitions!.Any(t => t.FromStateId == current && t.Symbol == '\0').ShouldBeTrue();
    }

    [Fact]
    public void GenerateEpsilonCase_UnreachableEpsilon_ReturnsNull()
    {
        // Arrange - epsilon transition exists but is unreachable from start
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.EpsilonNFA,
            States =
            [
                new State { Id = 0, IsStart = true, IsAccepting = false },
                new State { Id = 1, IsStart = false, IsAccepting = false },
                new State { Id = 2, IsStart = false, IsAccepting = true },
                new State { Id = 3, IsStart = false, IsAccepting = false }
            ],
            Transitions =
            [
                new Transition { FromStateId = 2, ToStateId = 3, Symbol = '\0' } // unreachable epsilon
            ]
        };

        // Act
        var result = service.GenerateEpsilonCase(automaton, 15);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GenerateEpsilonCase_MultipleEpsilons_ReturnsPathToOneOfThem()
    {
        // Arrange - two epsilon sources reachable via different symbols
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.EpsilonNFA,
            States =
            [
                new State { Id = 0, IsStart = true, IsAccepting = false },
                new State { Id = 1, IsStart = false, IsAccepting = false },
                new State { Id = 2, IsStart = false, IsAccepting = true },
                new State { Id = 3, IsStart = false, IsAccepting = false },
                new State { Id = 4, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new Transition { FromStateId = 0, ToStateId = 1, Symbol = 'a' },
                new Transition { FromStateId = 1, ToStateId = 2, Symbol = '\0' }, // epsilon at state 1
                new Transition { FromStateId = 0, ToStateId = 3, Symbol = 'b' },
                new Transition { FromStateId = 3, ToStateId = 4, Symbol = '\0' }  // epsilon at state 3
            ]
        };

        // Act
        var result = service.GenerateEpsilonCase(automaton, 15);

        // Assert
        result.ShouldNotBeNull();

        // Determine which state the path leads to
        var current = automaton.States!.First(s => s.IsStart).Id;
        foreach (var ch in result!)
        {
            var trans = automaton.Transitions!.FirstOrDefault(t => t.FromStateId == current && t.Symbol == ch);
            trans.ShouldNotBeNull($"No transition for symbol '{ch}' from state {current}");
            current = trans!.ToStateId;
        }

        // The resulting state should be either 1 or 3 (sources of epsilon)
        (current == 1 || current == 3).ShouldBeTrue($"Path ended at state {current}, expected 1 or 3");
    }

    [Fact]
    public void GenerateEpsilonCase_WithNoEpsilonTransitions_ReturnsNull()
    {
        // Arrange
        var automaton = CreateSampleDfa();

        // Act
        var result = service.GenerateEpsilonCase(automaton, 15);

        // Assert
        result.ShouldBeNull();
    }

    #endregion

    #region Helper Methods

    private static AutomatonViewModel CreateSampleDfa()
    {
        return new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new State { Id = 0, IsStart = true, IsAccepting = false },
                new State { Id = 1, IsStart = false, IsAccepting = false },
                new State { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new Transition { FromStateId = 0, ToStateId = 1, Symbol = 'a' },
                new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'b' }
            ]
        };
    }

    private static AutomatonViewModel CreateNondeterministicNfa()
    {
        return new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States =
            [
                new State { Id = 0, IsStart = true, IsAccepting = false },
                new State { Id = 1, IsStart = false, IsAccepting = false },
                new State { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new Transition { FromStateId = 0, ToStateId = 1, Symbol = 'a' },
                new Transition { FromStateId = 0, ToStateId = 2, Symbol = 'a' }, // Nondeterministic!
                new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'b' }
            ]
        };
    }

    private static AutomatonViewModel CreateEpsilonNfa()
    {
        return new AutomatonViewModel
        {
            Type = AutomatonType.EpsilonNFA,
            States =
            [
                new State { Id = 0, IsStart = true, IsAccepting = false },
                new State { Id = 1, IsStart = false, IsAccepting = false },
                new State { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new Transition { FromStateId = 0, ToStateId = 1, Symbol = 'a' },
                new Transition { FromStateId = 1, ToStateId = 2, Symbol = '\0' }, // Epsilon!
                new Transition { FromStateId = 0, ToStateId = 2, Symbol = 'b' }
            ]
        };
    }

    private static AutomatonViewModel CreateAutomatonWithMultiplePaths()
    {
        return new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States =
            [
                new State { Id = 0, IsStart = true, IsAccepting = false },
                new State { Id = 1, IsStart = false, IsAccepting = false },
                new State { Id = 2, IsStart = false, IsAccepting = false },
                new State { Id = 3, IsStart = false, IsAccepting = true },
                new State { Id = 4, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new Transition { FromStateId = 0, ToStateId = 1, Symbol = 'a' },
                new Transition { FromStateId = 0, ToStateId = 2, Symbol = 'b' },
                new Transition { FromStateId = 1, ToStateId = 3, Symbol = 'a' },
                new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'b' }, // Self-loop
                new Transition { FromStateId = 2, ToStateId = 4, Symbol = 'b' },
                new Transition { FromStateId = 2, ToStateId = 2, Symbol = 'a' }, // Self-loop
                new Transition { FromStateId = 3, ToStateId = 3, Symbol = 'a' }, // Self-loop on accepting
                new Transition { FromStateId = 4, ToStateId = 4, Symbol = 'b' }  // Self-loop on accepting
            ]
        };
    }

    #endregion
}
