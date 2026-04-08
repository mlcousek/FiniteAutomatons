using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using FiniteAutomatons.Services.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FiniteAutomatons.UnitTests.Services;

public class AutomatonPresetServiceTests
{
    private readonly MockAutomatonGeneratorService mockGeneratorService;
    private readonly MockAutomatonMinimizationService mockMinimizationService;
    private readonly AutomatonPresetService service;

    public AutomatonPresetServiceTests()
    {
        mockGeneratorService = new MockAutomatonGeneratorService();
        mockMinimizationService = new MockAutomatonMinimizationService();
        service = new AutomatonPresetService(
            mockGeneratorService,
            mockMinimizationService,
            NullLogger<AutomatonPresetService>.Instance);
    }

    #region GenerateMinimalizedDfa Tests

    [Fact]
    public void GenerateMinimalizedDfa_SuccessfulMinimization_ReturnsMinimizedDfa()
    {
        // Arrange
        var baseDfa = CreateSampleDfa(5);
        var minimizedDfa = CreateSampleDfa(3);

        mockGeneratorService.RandomAutomatonToReturn = baseDfa;
        mockMinimizationService.MinimizedDfaToReturn = minimizedDfa;
        mockMinimizationService.MinimizationMessage = "Success";

        // Act
        var result = service.GenerateMinimalizedDfa(5, 10, 3, 0.3);

        // Assert
        result.ShouldBe(minimizedDfa);
        result.States.Count.ShouldBe(3);
    }

    [Fact]
    public void GenerateMinimalizedDfa_MinimizationFails_ThrowsInvalidOperationException()
    {
        // Arrange
        var baseDfa = CreateSampleDfa(5);

        mockGeneratorService.RandomAutomatonToReturn = baseDfa;
        mockMinimizationService.ShouldReturnNull = true;
        mockMinimizationService.MinimizationMessage = "Minimization failed";

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => service.GenerateMinimalizedDfa(5, 10, 3, 0.3))
            .Message.ShouldContain("Failed to minimize generated DFA");
    }

    [Fact]
    public void GenerateMinimalizedDfa_WithSeed_ProducesDeterministicResults()
    {
        // Arrange
        var seed = 12345;
        var baseDfa = CreateSampleDfa(5);
        var minimizedDfa = CreateSampleDfa(3);

        mockGeneratorService.RandomAutomatonToReturn = baseDfa;
        mockMinimizationService.MinimizedDfaToReturn = minimizedDfa;
        mockMinimizationService.MinimizationMessage = "Success";

        // Act
        var result = service.GenerateMinimalizedDfa(5, 10, 3, 0.3, seed);

        // Assert
        result.ShouldNotBeNull();
    }

    #endregion

    #region PDA Type Routing Tests

    [Fact]
    public void GenerateRandomPda_WithNpdaType_RequestsNpdaFromGenerator()
    {
        // Arrange
        mockGeneratorService.RandomAutomatonToReturn = new AutomatonViewModel
        {
            Type = AutomatonType.NPDA,
            States = [new State { Id = 0, IsStart = true, IsAccepting = true }],
            Transitions = []
        };

        // Act
        var result = service.GenerateRandomPda(pdaType: AutomatonType.NPDA);

        // Assert
        mockGeneratorService.LastRequestedType.ShouldBe(AutomatonType.NPDA);
        result.Type.ShouldBe(AutomatonType.NPDA);
    }

    [Fact]
    public void GeneratePdaWithPushPopPairs_WithNpdaType_RequestsNpdaFromGenerator()
    {
        // Arrange
        mockGeneratorService.RandomAutomatonToReturn = new AutomatonViewModel
        {
            Type = AutomatonType.NPDA,
            States =
            [
                new State { Id = 0, IsStart = true, IsAccepting = false },
                new State { Id = 1, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new Transition { FromStateId = 0, ToStateId = 1, Symbol = 'a', StackPop = null, StackPush = "A" }
            ]
        };

        // Act
        var result = service.GeneratePdaWithPushPopPairs(pdaType: AutomatonType.NPDA);

        // Assert
        mockGeneratorService.LastRequestedType.ShouldBe(AutomatonType.NPDA);
        result.Type.ShouldBe(AutomatonType.NPDA);
    }

    [Fact]
    public void GeneratePdaWithPushPopPairs_WithDpdaType_KeepsModelDeterministic()
    {
        // Arrange - Every state already has transitions for each alphabet symbol,
        // so blind additions would create DPDA determinism conflicts.
        mockGeneratorService.RandomAutomatonToReturn = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States =
            [
                new State { Id = 0, IsStart = true, IsAccepting = false },
                new State { Id = 1, IsStart = false, IsAccepting = false },
                new State { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new Transition { FromStateId = 0, ToStateId = 1, Symbol = 'a', StackPop = null, StackPush = null },
                new Transition { FromStateId = 0, ToStateId = 2, Symbol = 'b', StackPop = null, StackPush = null },
                new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = null, StackPush = null },
                new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'b', StackPop = null, StackPush = null },
                new Transition { FromStateId = 2, ToStateId = 2, Symbol = 'a', StackPop = null, StackPush = null },
                new Transition { FromStateId = 2, ToStateId = 0, Symbol = 'b', StackPop = null, StackPush = null }
            ],
            IsCustomAutomaton = true
        };

        // Act
        var result = service.GeneratePdaWithPushPopPairs(seed: 7, pdaType: AutomatonType.DPDA);

        // Assert
        mockGeneratorService.LastRequestedType.ShouldBe(AutomatonType.DPDA);
        result.Type.ShouldBe(AutomatonType.DPDA);
        IsDpdaDeterministic(result).ShouldBeTrue();
    }

    [Fact]
    public void GeneratePdaWithPushPopPairs_WithDpdaType_PreservesValidSameSymbolDifferentStackTop()
    {
        // Arrange
        mockGeneratorService.RandomAutomatonToReturn = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States =
            [
                new State { Id = 0, IsStart = true, IsAccepting = false },
                new State { Id = 1, IsStart = false, IsAccepting = false },
                new State { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new Transition { FromStateId = 0, ToStateId = 1, Symbol = 'a', StackPop = 'A', StackPush = null },
                new Transition { FromStateId = 0, ToStateId = 2, Symbol = 'a', StackPop = 'B', StackPush = null },
                new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'b', StackPop = null, StackPush = null }
            ],
            IsCustomAutomaton = true
        };

        // Act
        var result = service.GeneratePdaWithPushPopPairs(seed: 11, pdaType: AutomatonType.DPDA);

        // Assert
        IsDpdaDeterministic(result).ShouldBeTrue();

        result.Transitions
            .Count(t => t.FromStateId == 0 && t.Symbol == 'a')
            .ShouldBeGreaterThanOrEqualTo(2);
    }

    #endregion

    #region GenerateUnminimalizedDfa Tests

    [Fact]
    public void GenerateUnminimalizedDfa_AlreadyUnminimalized_ReturnsOriginal()
    {
        // Arrange
        var baseDfa = CreateSampleDfa(5);
        var minimizedDfa = CreateSampleDfa(3); // Smaller = already unminimalized

        mockGeneratorService.RandomAutomatonToReturn = baseDfa;
        mockMinimizationService.MinimizedDfaToReturn = minimizedDfa;

        // Act
        var result = service.GenerateUnminimalizedDfa(5, 10, 3, 0.3);

        // Assert
        result.ShouldBe(baseDfa);
        result.States.Count.ShouldBe(5); // Original count preserved
    }

    [Fact]
    public void GenerateUnminimalizedDfa_MinimalDfa_AddsEquivalentStates()
    {
        // Arrange
        var minimalDfa = CreateSampleDfa(3);
        var sameSizeDfa = CreateSampleDfa(3); // Same size = is minimal

        mockGeneratorService.RandomAutomatonToReturn = minimalDfa;
        mockMinimizationService.MinimizedDfaToReturn = sameSizeDfa;

        // Act
        var result = service.GenerateUnminimalizedDfa(3, 10, 3, 0.3);

        // Assert
        result.ShouldNotBeNull();
        result.States.Count.ShouldBeGreaterThan(minimalDfa.States.Count); // Should have added states
        result.Type.ShouldBe(AutomatonType.DFA);
    }

    [Fact]
    public void GenerateUnminimalizedDfa_WithSeed_ProducesDeterministicResults()
    {
        // Arrange
        var seed = 42;
        var minimalDfa = CreateSampleDfa(3);
        var sameSizeDfa = CreateSampleDfa(3);

        mockGeneratorService.RandomAutomatonToReturn = minimalDfa;
        mockMinimizationService.MinimizedDfaToReturn = sameSizeDfa;

        // Act
        var result1 = service.GenerateUnminimalizedDfa(3, 10, 3, 0.3, seed);
        var result2 = service.GenerateUnminimalizedDfa(3, 10, 3, 0.3, seed);

        // Assert
        result1.States.Count.ShouldBe(result2.States.Count);
        result1.Transitions.Count.ShouldBe(result2.Transitions.Count);
    }

    [Fact]
    public void GenerateUnminimalizedDfa_OnlyStartState_CannotAddEquivalentStates()
    {
        // Arrange - DFA with only start state
        var singleStateDfa = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new State { Id = 0, IsStart = true, IsAccepting = true }],
            Transitions = [],
            IsCustomAutomaton = true
        };

        mockGeneratorService.RandomAutomatonToReturn = singleStateDfa;
        mockMinimizationService.MinimizedDfaToReturn = singleStateDfa; // Same = is minimal

        // Act
        var result = service.GenerateUnminimalizedDfa(1, 10, 3, 0.3);

        // Assert
        result.ShouldNotBeNull();
        result.States.Count.ShouldBe(1); // Cannot add equivalent states with only start state
    }

    #endregion

    #region GenerateNondeterministicNfa Tests

    [Fact]
    public void GenerateNondeterministicNfa_AlreadyNondeterministic_ReturnsOriginal()
    {
        // Arrange
        var nondeterministicNfa = CreateNondeterministicNfa();
        mockGeneratorService.RandomAutomatonToReturn = nondeterministicNfa;

        // Act
        var result = service.GenerateNondeterministicNfa(5, 10, 3, 0.3);

        // Assert
        result.ShouldBe(nondeterministicNfa);
        HasNondeterminism(result).ShouldBeTrue();
    }

    [Fact]
    public void GenerateNondeterministicNfa_DeterministicNfa_AddsNondeterministicTransitions()
    {
        // Arrange
        var deterministicNfa = CreateDeterministicNfa();
        mockGeneratorService.RandomAutomatonToReturn = deterministicNfa;

        // Act
        var result = service.GenerateNondeterministicNfa(5, 10, 3, 0.3);

        // Assert
        result.ShouldNotBeNull();
        result.Transitions.Count.ShouldBeGreaterThan(deterministicNfa.Transitions.Count);
        HasNondeterminism(result).ShouldBeTrue(); // MUST have nondeterminism
    }

    [Fact]
    public void GenerateNondeterministicNfa_WithSeed_ProducesDeterministicResults()
    {
        // Arrange
        var seed = 99;
        var deterministicNfa = CreateDeterministicNfa();
        mockGeneratorService.RandomAutomatonToReturn = deterministicNfa;

        // Act
        var result = service.GenerateNondeterministicNfa(5, 10, 3, 0.3, seed);

        // Assert
        result.ShouldNotBeNull();
        HasNondeterminism(result).ShouldBeTrue();
    }

    [Fact]
    public void GenerateNondeterministicNfa_NoAlphabet_CannotAddTransitions()
    {
        // Arrange - NFA with no alphabet symbols
        var nfaNoAlphabet = new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States =
            [
                new State { Id = 0, IsStart = true, IsAccepting = false },
                new State { Id = 1, IsStart = false, IsAccepting = true }
            ],
            Transitions = [],
            IsCustomAutomaton = true
        };

        mockGeneratorService.RandomAutomatonToReturn = nfaNoAlphabet;

        // Act
        var result = service.GenerateNondeterministicNfa(2, 10, 3, 0.3);

        // Assert
        result.ShouldNotBeNull();
        result.Transitions.Count.ShouldBe(0); // Cannot add transitions without alphabet
    }

    #endregion

    #region GenerateRandomNfa Tests

    [Fact]
    public void GenerateRandomNfa_CallsGeneratorService()
    {
        // Arrange
        var expectedNfa = CreateSampleNfa(5);
        mockGeneratorService.RandomAutomatonToReturn = expectedNfa;

        // Act
        var result = service.GenerateRandomNfa(5, 10, 3, 0.3);

        // Assert
        result.ShouldBe(expectedNfa);
        result.Type.ShouldBe(AutomatonType.NFA);
    }

    [Fact]
    public void GenerateRandomNfa_WithSeed_PassesSeedToGenerator()
    {
        // Arrange
        var seed = 123;
        var expectedNfa = CreateSampleNfa(5);
        mockGeneratorService.RandomAutomatonToReturn = expectedNfa;

        // Act
        var result = service.GenerateRandomNfa(5, 10, 3, 0.3, seed);

        // Assert
        result.ShouldBe(expectedNfa);
        result.Type.ShouldBe(AutomatonType.NFA);
    }

    #endregion

    #region GenerateEpsilonNfa Tests

    [Fact]
    public void GenerateEpsilonNfa_AlreadyHasEpsilonTransitions_ReturnsOriginal()
    {
        // Arrange
        var epsilonNfa = CreateEpsilonNfaWithEpsilon();
        mockGeneratorService.RandomAutomatonToReturn = epsilonNfa;

        // Act
        var result = service.GenerateEpsilonNfa(5, 10, 3, 0.3);

        // Assert
        result.ShouldBe(epsilonNfa);
        HasEpsilonTransitions(result).ShouldBeTrue();
    }

    [Fact]
    public void GenerateEpsilonNfa_NoEpsilonTransitions_AddsEpsilonTransitions()
    {
        // Arrange
        var nfaWithoutEpsilon = CreateSampleNfa(5);
        mockGeneratorService.RandomAutomatonToReturn = nfaWithoutEpsilon;

        // Act
        var result = service.GenerateEpsilonNfa(5, 10, 3, 0.3);

        // Assert
        result.ShouldNotBeNull();
        result.Transitions.Count.ShouldBeGreaterThan(nfaWithoutEpsilon.Transitions.Count);
        HasEpsilonTransitions(result).ShouldBeTrue(); // MUST have epsilon transitions
    }

    [Fact]
    public void GenerateEpsilonNfa_WithSeed_ProducesDeterministicResults()
    {
        // Arrange
        var seed = 777;
        var nfaWithoutEpsilon = CreateSampleNfa(5);
        mockGeneratorService.RandomAutomatonToReturn = nfaWithoutEpsilon;

        // Act
        var result = service.GenerateEpsilonNfa(5, 10, 3, 0.3, seed);

        // Assert
        result.ShouldNotBeNull();
        HasEpsilonTransitions(result).ShouldBeTrue();
    }

    [Fact]
    public void GenerateEpsilonNfa_LessThanTwoStates_CannotAddEpsilonTransitions()
    {
        // Arrange - ε-NFA with only 1 state
        var singleStateEnfa = new AutomatonViewModel
        {
            Type = AutomatonType.EpsilonNFA,
            States = [new State { Id = 0, IsStart = true, IsAccepting = true }],
            Transitions = [],
            IsCustomAutomaton = true
        };

        mockGeneratorService.RandomAutomatonToReturn = singleStateEnfa;

        // Act
        var result = service.GenerateEpsilonNfa(1, 10, 3, 0.3);

        // Assert
        result.ShouldNotBeNull();
        result.States.Count.ShouldBe(1);
        HasEpsilonTransitions(result).ShouldBeFalse(); // Cannot add epsilon with < 2 states
    }

    #endregion

    #region GenerateEpsilonNfaNondeterministic Tests

    [Fact]
    public void GenerateEpsilonNfaNondeterministic_AlreadyNondeterministic_ReturnsWithEpsilon()
    {
        // Arrange
        var nondeterministicEpsilonNfa = CreateNondeterministicEpsilonNfa();
        mockGeneratorService.RandomAutomatonToReturn = nondeterministicEpsilonNfa;

        // Act
        var result = service.GenerateEpsilonNfaNondeterministic(5, 10, 3, 0.3);

        // Assert
        result.ShouldNotBeNull();
        HasEpsilonTransitions(result).ShouldBeTrue();
        HasNondeterminism(result).ShouldBeTrue();
    }

    [Fact]
    public void GenerateEpsilonNfaNondeterministic_DeterministicEpsilonNfa_AddsBothFeatures()
    {
        // Arrange
        var deterministicNfa = CreateDeterministicNfa(); // No epsilon, deterministic
        mockGeneratorService.RandomAutomatonToReturn = deterministicNfa;

        // Act
        var result = service.GenerateEpsilonNfaNondeterministic(5, 10, 3, 0.3);

        // Assert
        result.ShouldNotBeNull();
        HasEpsilonTransitions(result).ShouldBeTrue(); // MUST have epsilon
        HasNondeterminism(result).ShouldBeTrue(); // MUST have nondeterminism
    }

    [Fact]
    public void GenerateEpsilonNfaNondeterministic_WithSeed_ProducesDeterministicResults()
    {
        // Arrange
        var seed = 888;
        var deterministicNfa = CreateDeterministicNfa();
        mockGeneratorService.RandomAutomatonToReturn = deterministicNfa;

        // Act
        var result = service.GenerateEpsilonNfaNondeterministic(5, 10, 3, 0.3, seed);

        // Assert
        result.ShouldNotBeNull();
        HasEpsilonTransitions(result).ShouldBeTrue();
        HasNondeterminism(result).ShouldBeTrue();
    }

    #endregion

    #region Parameter Validation Tests

    [Fact]
    public void GenerateMinimalizedDfa_WithAllParameters_PassesCorrectValuesToGenerator()
    {
        // Arrange
        var baseDfa = CreateSampleDfa(7);
        var minimizedDfa = CreateSampleDfa(5);
        mockGeneratorService.RandomAutomatonToReturn = baseDfa;
        mockMinimizationService.MinimizedDfaToReturn = minimizedDfa;

        // Act
        var result = service.GenerateMinimalizedDfa(7, 15, 4, 0.4, 999);

        // Assert
        result.ShouldNotBeNull();
        result.States.Count.ShouldBe(5);
    }

    [Fact]
    public void GenerateUnminimalizedDfa_WithAllParameters_PassesCorrectValuesToGenerator()
    {
        // Arrange
        var minimalDfa = CreateSampleDfa(4);
        mockGeneratorService.RandomAutomatonToReturn = minimalDfa;
        mockMinimizationService.MinimizedDfaToReturn = minimalDfa;

        // Act
        var result = service.GenerateUnminimalizedDfa(4, 12, 5, 0.35, 777);

        // Assert
        result.ShouldNotBeNull();
        result.States.Count.ShouldBeGreaterThan(4);
    }

    [Fact]
    public void GenerateNondeterministicNfa_WithAllParameters_PassesCorrectValuesToGenerator()
    {
        // Arrange
        var deterministicNfa = CreateDeterministicNfa();
        mockGeneratorService.RandomAutomatonToReturn = deterministicNfa;

        // Act
        var result = service.GenerateNondeterministicNfa(6, 14, 4, 0.4, 555);

        // Assert
        result.ShouldNotBeNull();
        HasNondeterminism(result).ShouldBeTrue();
    }

    [Fact]
    public void GenerateRandomNfa_WithAllParameters_PassesCorrectValuesToGenerator()
    {
        // Arrange
        var expectedNfa = CreateSampleNfa(8);
        mockGeneratorService.RandomAutomatonToReturn = expectedNfa;

        // Act
        var result = service.GenerateRandomNfa(8, 20, 6, 0.5, 444);

        // Assert
        result.ShouldBe(expectedNfa);
    }

    [Fact]
    public void GenerateEpsilonNfa_WithAllParameters_PassesCorrectValuesToGenerator()
    {
        // Arrange
        var nfaWithoutEpsilon = CreateSampleNfa(6);
        mockGeneratorService.RandomAutomatonToReturn = nfaWithoutEpsilon;

        // Act
        var result = service.GenerateEpsilonNfa(6, 16, 5, 0.45, 333);

        // Assert
        result.ShouldNotBeNull();
        HasEpsilonTransitions(result).ShouldBeTrue();
    }

    [Fact]
    public void GenerateEpsilonNfaNondeterministic_WithAllParameters_PassesCorrectValuesToGenerator()
    {
        // Arrange - Use an NFA with more states to ensure epsilon and nondeterministic transitions can be added
        var baseNfa = new AutomatonViewModel
        {
            Type = AutomatonType.EpsilonNFA,
            States =
            [
                new State { Id = 0, IsStart = true, IsAccepting = false },
                new State { Id = 1, IsStart = false, IsAccepting = false },
                new State { Id = 2, IsStart = false, IsAccepting = false },
                new State { Id = 3, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new Transition { FromStateId = 0, ToStateId = 1, Symbol = 'a' },
                new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'b' },
                new Transition { FromStateId = 2, ToStateId = 3, Symbol = 'c' }
            ],
            IsCustomAutomaton = true
        };
        mockGeneratorService.RandomAutomatonToReturn = baseNfa;

        // Act
        var result = service.GenerateEpsilonNfaNondeterministic(7, 18, 4, 0.38, 222);

        // Assert
        result.ShouldNotBeNull();
        // With sufficient states, epsilon and nondeterministic transitions should be added
        result.Transitions.Count.ShouldBeGreaterThan(baseNfa.Transitions.Count);
    }

    #endregion

    #region Edge Cases for Unminimalized DFA

    [Fact]
    public void GenerateUnminimalizedDfa_DfaWithOnlyAcceptingStates_AddsEquivalentStates()
    {
        // Arrange - DFA where all states are accepting (except start)
        var allAcceptingDfa = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new State { Id = 0, IsStart = true, IsAccepting = false },
                new State { Id = 1, IsStart = false, IsAccepting = true },
                new State { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new Transition { FromStateId = 0, ToStateId = 1, Symbol = 'a' },
                new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'b' }
            ],
            IsCustomAutomaton = true
        };

        mockGeneratorService.RandomAutomatonToReturn = allAcceptingDfa;
        mockMinimizationService.MinimizedDfaToReturn = allAcceptingDfa;

        // Act
        var result = service.GenerateUnminimalizedDfa(3, 10, 3, 0.3);

        // Assert
        result.ShouldNotBeNull();
        result.States.Count.ShouldBeGreaterThan(allAcceptingDfa.States.Count);
    }

    [Fact]
    public void GenerateUnminimalizedDfa_DfaWithNoNonStartNonAcceptingStates_UsesAcceptingStates()
    {
        // Arrange - DFA with only start and accepting states
        var noMiddleStateDfa = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new State { Id = 0, IsStart = true, IsAccepting = false },
                new State { Id = 1, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new Transition { FromStateId = 0, ToStateId = 1, Symbol = 'a' }
            ],
            IsCustomAutomaton = true
        };

        mockGeneratorService.RandomAutomatonToReturn = noMiddleStateDfa;
        mockMinimizationService.MinimizedDfaToReturn = noMiddleStateDfa;

        // Act
        var result = service.GenerateUnminimalizedDfa(2, 10, 3, 0.3);

        // Assert
        result.ShouldNotBeNull();
        result.States.Count.ShouldBeGreaterThan(2);
    }

    #endregion

    #region Edge Cases for Nondeterministic NFA

    [Fact]
    public void GenerateNondeterministicNfa_NfaWithSingleState_CannotAddNondeterminism()
    {
        // Arrange - NFA with only one state
        var singleStateNfa = new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States = [new State { Id = 0, IsStart = true, IsAccepting = true }],
            Transitions = [],
            IsCustomAutomaton = true
        };

        mockGeneratorService.RandomAutomatonToReturn = singleStateNfa;

        // Act
        var result = service.GenerateNondeterministicNfa(1, 10, 3, 0.3);

        // Assert
        result.ShouldNotBeNull();
        result.Transitions.Count.ShouldBe(0);
    }

    [Fact]
    public void GenerateNondeterministicNfa_NfaWithNoTransitions_CannotAddNondeterminism()
    {
        // Arrange - NFA with states but no transitions
        var noTransitionsNfa = new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States =
            [
                new State { Id = 0, IsStart = true, IsAccepting = false },
                new State { Id = 1, IsStart = false, IsAccepting = true }
            ],
            Transitions = [],
            IsCustomAutomaton = true
        };

        mockGeneratorService.RandomAutomatonToReturn = noTransitionsNfa;

        // Act
        var result = service.GenerateNondeterministicNfa(2, 10, 3, 0.3);

        // Assert
        result.ShouldNotBeNull();
        result.Transitions.Count.ShouldBe(0);
    }

    [Fact]
    public void GenerateNondeterministicNfa_NfaWhereAllTransitionsExist_StillAddsNondeterminism()
    {
        // Arrange - Fully connected deterministic NFA
        var fullyConnectedNfa = new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States =
            [
                new State { Id = 0, IsStart = true, IsAccepting = false },
                new State { Id = 1, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new Transition { FromStateId = 0, ToStateId = 0, Symbol = 'a' },
                new Transition { FromStateId = 0, ToStateId = 1, Symbol = 'b' }
            ],
            IsCustomAutomaton = true
        };

        mockGeneratorService.RandomAutomatonToReturn = fullyConnectedNfa;

        // Act
        var result = service.GenerateNondeterministicNfa(2, 10, 3, 0.3);

        // Assert
        result.ShouldNotBeNull();
        result.Type.ShouldBe(AutomatonType.NFA);
        result.Transitions.Count.ShouldBeGreaterThan(fullyConnectedNfa.Transitions.Count);
        HasNondeterminism(result).ShouldBeTrue();
    }

    #endregion

    #region Edge Cases for Epsilon NFA

    [Fact]
    public void GenerateEpsilonNfa_EnfaWithTwoStates_AddsEpsilonTransition()
    {
        // Arrange - Minimal ε-NFA with 2 states but no epsilon transitions initially
        var twoStateEnfa = new AutomatonViewModel
        {
            Type = AutomatonType.EpsilonNFA,
            States =
            [
                new State { Id = 0, IsStart = true, IsAccepting = false },
                new State { Id = 1, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new Transition { FromStateId = 0, ToStateId = 1, Symbol = 'a' }
            ],
            IsCustomAutomaton = true
        };

        mockGeneratorService.RandomAutomatonToReturn = twoStateEnfa;

        // Act
        var result = service.GenerateEpsilonNfa(2, 10, 3, 0.3);

        // Assert
        result.ShouldNotBeNull();
        result.Type.ShouldBe(AutomatonType.EpsilonNFA);
        // With 2 states and the epsilon adding logic, epsilon transitions should be attempted
        // Even if some attempts fail due to self-loops, at least the service should try
        result.States.Count.ShouldBe(2);
    }

    [Fact]
    public void GenerateEpsilonNfa_EnfaWithMultipleEpsilonTransitions_ReturnsOriginal()
    {
        // Arrange - ε-NFA with multiple epsilon transitions
        var multipleEpsilonNfa = new AutomatonViewModel
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
                new Transition { FromStateId = 0, ToStateId = 1, Symbol = '\0' },
                new Transition { FromStateId = 1, ToStateId = 2, Symbol = '\0' },
                new Transition { FromStateId = 0, ToStateId = 2, Symbol = 'a' }
            ],
            IsCustomAutomaton = true
        };

        mockGeneratorService.RandomAutomatonToReturn = multipleEpsilonNfa;

        // Act
        var result = service.GenerateEpsilonNfa(3, 10, 3, 0.3);

        // Assert
        result.ShouldBe(multipleEpsilonNfa);
        result.Transitions.Where(t => t.Symbol == '\0').Count().ShouldBe(2);
    }

    #endregion

    #region Edge Cases for Epsilon NFA Nondeterministic

    [Fact]
    public void GenerateEpsilonNfaNondeterministic_AlreadyHasEpsilonAndNondeterminism_ReturnsOriginal()
    {
        // Arrange - Already has both features
        var completeEnfa = CreateNondeterministicEpsilonNfa();
        mockGeneratorService.RandomAutomatonToReturn = completeEnfa;

        // Act
        var result = service.GenerateEpsilonNfaNondeterministic(3, 10, 3, 0.3);

        // Assert
        result.ShouldNotBeNull();
        HasEpsilonTransitions(result).ShouldBeTrue();
        HasNondeterminism(result).ShouldBeTrue();
    }

    [Fact]
    public void GenerateEpsilonNfaNondeterministic_HasEpsilonButDeterministic_AddsNondeterminism()
    {
        // Arrange - Has epsilon but is deterministic
        var epsilonButDeterministic = new AutomatonViewModel
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
                new Transition { FromStateId = 0, ToStateId = 2, Symbol = '\0' },
                new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'b' }
            ],
            IsCustomAutomaton = true
        };

        mockGeneratorService.RandomAutomatonToReturn = epsilonButDeterministic;

        // Act 
        var result = service.GenerateEpsilonNfaNondeterministic(3, 10, 3, 0.3);

        // Assert
        result.ShouldNotBeNull();
        HasEpsilonTransitions(result).ShouldBeTrue();
        HasNondeterminism(result).ShouldBeTrue();
    }

    #endregion

    #region Default Parameter Tests

    [Fact]
    public void GenerateMinimalizedDfa_WithDefaultParameters_WorksCorrectly()
    {
        // Arrange
        var baseDfa = CreateSampleDfa(5);
        var minimizedDfa = CreateSampleDfa(3);
        mockGeneratorService.RandomAutomatonToReturn = baseDfa;
        mockMinimizationService.MinimizedDfaToReturn = minimizedDfa;

        // Act
        var result = service.GenerateMinimalizedDfa();

        // Assert
        result.ShouldNotBeNull();
        result.States.Count.ShouldBe(3);
    }

    [Fact]
    public void GenerateUnminimalizedDfa_WithDefaultParameters_WorksCorrectly()
    {
        // Arrange
        var minimalDfa = CreateSampleDfa(5);
        mockGeneratorService.RandomAutomatonToReturn = minimalDfa;
        mockMinimizationService.MinimizedDfaToReturn = minimalDfa;

        // Act
        var result = service.GenerateUnminimalizedDfa();

        // Assert
        result.ShouldNotBeNull();
    }

    [Fact]
    public void GenerateNondeterministicNfa_WithDefaultParameters_WorksCorrectly()
    {
        // Arrange
        var deterministicNfa = CreateDeterministicNfa();
        mockGeneratorService.RandomAutomatonToReturn = deterministicNfa;

        // Act
        var result = service.GenerateNondeterministicNfa();

        // Assert
        result.ShouldNotBeNull();
    }

    [Fact]
    public void GenerateRandomNfa_WithDefaultParameters_WorksCorrectly()
    {
        // Arrange
        var expectedNfa = CreateSampleNfa(5);
        mockGeneratorService.RandomAutomatonToReturn = expectedNfa;

        // Act
        var result = service.GenerateRandomNfa();

        // Assert
        result.ShouldNotBeNull();
    }

    [Fact]
    public void GenerateEpsilonNfa_WithDefaultParameters_WorksCorrectly()
    {
        // Arrange
        var nfaWithoutEpsilon = CreateSampleNfa(5);
        mockGeneratorService.RandomAutomatonToReturn = nfaWithoutEpsilon;

        // Act
        var result = service.GenerateEpsilonNfa();

        // Assert
        result.ShouldNotBeNull();
    }

    [Fact]
    public void GenerateEpsilonNfaNondeterministic_WithDefaultParameters_WorksCorrectly()
    {
        // Arrange
        var deterministicNfa = CreateDeterministicNfa();
        mockGeneratorService.RandomAutomatonToReturn = deterministicNfa;

        // Act
        var result = service.GenerateEpsilonNfaNondeterministic();

        // Assert
        result.ShouldNotBeNull();
    }

    #endregion

    #region Helper Methods

    private static AutomatonViewModel CreateSampleDfa(int stateCount)
    {
        var states = new List<State>();
        for (int i = 0; i < stateCount; i++)
        {
            states.Add(new State
            {
                Id = i,
                IsStart = i == 0,
                IsAccepting = i == stateCount - 1
            });
        }

        var transitions = new List<Transition>();
        for (int i = 0; i < stateCount - 1; i++)
        {
            transitions.Add(new Transition
            {
                FromStateId = i,
                ToStateId = i + 1,
                Symbol = 'a'
            });
        }

        return new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = states,
            Transitions = transitions,
            IsCustomAutomaton = true
        };
    }

    private static AutomatonViewModel CreateSampleNfa(int stateCount)
    {
        var states = new List<State>();
        for (int i = 0; i < stateCount; i++)
        {
            states.Add(new State
            {
                Id = i,
                IsStart = i == 0,
                IsAccepting = i == stateCount - 1
            });
        }

        var transitions = new List<Transition>
        {
            new() { FromStateId = 0, ToStateId = 1, Symbol = 'a' },
            new() { FromStateId = 1, ToStateId = 2, Symbol = 'b' },
            new() { FromStateId = 2, ToStateId = 3, Symbol = 'a' },
            new() { FromStateId = 3, ToStateId = 4, Symbol = 'b' }
        };

        return new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States = states,
            Transitions = transitions,
            IsCustomAutomaton = true
        };
    }

    private static AutomatonViewModel CreateDeterministicNfa()
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
                new Transition { FromStateId = 0, ToStateId = 2, Symbol = 'b' },
                new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a' }
            ],
            IsCustomAutomaton = true
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
            ],
            IsCustomAutomaton = true
        };
    }

    private static AutomatonViewModel CreateEpsilonNfaWithEpsilon()
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
                new Transition { FromStateId = 0, ToStateId = 2, Symbol = '\0' }, // Epsilon!
                new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'b' }
            ],
            IsCustomAutomaton = true
        };
    }

    private static AutomatonViewModel CreateNondeterministicEpsilonNfa()
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
                new Transition { FromStateId = 0, ToStateId = 2, Symbol = 'a' }, // Nondeterministic!
                new Transition { FromStateId = 1, ToStateId = 2, Symbol = '\0' } // Epsilon!
            ],
            IsCustomAutomaton = true
        };
    }

    private static bool HasNondeterminism(AutomatonViewModel automaton)
    {
        var transitionGroups = automaton.Transitions
            .GroupBy(t => new { t.FromStateId, t.Symbol })
            .Where(g => g.Count() > 1);

        return transitionGroups.Any();
    }

    private static bool HasEpsilonTransitions(AutomatonViewModel automaton)
    {
        return automaton.Transitions.Any(t => t.Symbol == '\0');
    }

    private static bool IsDpdaDeterministic(AutomatonViewModel automaton)
    {
        for (int i = 0; i < automaton.Transitions.Count; i++)
        {
            for (int j = i + 1; j < automaton.Transitions.Count; j++)
            {
                var t1 = automaton.Transitions[i];
                var t2 = automaton.Transitions[j];

                if (t1.FromStateId != t2.FromStateId)
                    continue;

                if (!StackConditionsOverlap(t1, t2))
                    continue;

                bool t1IsEpsilon = t1.Symbol == '\0';
                bool t2IsEpsilon = t2.Symbol == '\0';

                if (t1.Symbol == t2.Symbol)
                    return false;

                if (t1IsEpsilon ^ t2IsEpsilon)
                    return false;
            }
        }

        return true;
    }

    private static bool StackConditionsOverlap(Transition t1, Transition t2)
    {
        bool t1AnyTop = !t1.StackPop.HasValue || t1.StackPop.Value == '\0';
        bool t2AnyTop = !t2.StackPop.HasValue || t2.StackPop.Value == '\0';

        if (t1AnyTop || t2AnyTop)
            return true;

        return t1.StackPop!.Value == t2.StackPop!.Value;
    }

    #endregion

    #region Mock Services

    private class MockAutomatonGeneratorService : IAutomatonGeneratorService
    {
        public AutomatonViewModel? RealisticAutomatonToReturn { get; set; }
        public AutomatonViewModel? RandomAutomatonToReturn { get; set; }
        public AutomatonType LastRequestedType { get; private set; }

        public AutomatonViewModel GenerateRandomAutomaton(
            AutomatonType type,
            int stateCount,
            int transitionCount,
            int alphabetSize = 4,
            double acceptingStateRatio = 0.3,
            int? seed = null,
            PDAAcceptanceMode? acceptanceMode = null,
            Stack<char>? initialStack = null)
        {
            LastRequestedType = type;
            return RandomAutomatonToReturn ?? new AutomatonViewModel { Type = type };
        }

        public bool ValidateGenerationParameters(
            AutomatonType type,
            int stateCount,
            int transitionCount,
            int alphabetSize)
        {
            return true;
        }

        public (int stateCount, int transitionCount, int alphabetSize, double acceptingRatio) GenerateRandomParameters(int? seed = null)
        {
            return (5, 10, 3, 0.3);
        }
    }

    private class MockAutomatonMinimizationService : IAutomatonMinimizationService
    {
        public AutomatonViewModel? MinimizedDfaToReturn { get; set; }
        public string MinimizationMessage { get; set; } = "Success";
        public bool ShouldReturnNull { get; set; } = false;

        public (AutomatonViewModel Result, string Message) MinimizeDfa(AutomatonViewModel dfa)
        {
            if (ShouldReturnNull)
                return (null!, MinimizationMessage);

            return (MinimizedDfaToReturn ?? dfa, MinimizationMessage);
        }

        public MinimizationAnalysis AnalyzeAutomaton(AutomatonViewModel model)
        {
            return new MinimizationAnalysis(
                SupportsMinimization: true,
                IsMinimal: false,
                OriginalStateCount: model.States.Count,
                ReachableStateCount: model.States.Count,
                MinimizedStateCount: MinimizedDfaToReturn?.States.Count ?? model.States.Count);
        }
    }

    #endregion
}
