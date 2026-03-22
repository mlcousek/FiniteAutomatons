using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using FiniteAutomatons.Services.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FiniteAutomatons.UnitTests.Services;

public class AutomatonMinimizationServiceTests
{
    private readonly AutomatonMinimizationService service;
    private readonly IAutomatonBuilderService builderService;
    private readonly IAutomatonAnalysisService analysisService;

    public AutomatonMinimizationServiceTests()
    {
        builderService = new AutomatonBuilderService(NullLogger<AutomatonBuilderService>.Instance);
        analysisService = new AutomatonAnalysisService();
        service = new AutomatonMinimizationService(
            builderService,
            analysisService,
            NullLogger<AutomatonMinimizationService>.Instance);
    }

    [Fact]
    public void MinimizeDfa_WithNonDFA_ReturnsErrorMessage()
    {
        // Arrange
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States = [new State { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = []
        };

        // Act
        var (result, message) = service.MinimizeDfa(model);

        // Assert
        message.ShouldBe("Minimization supported only for DFA.");
        result.ShouldBe(model);
    }

    [Fact]
    public void MinimizeDfa_WithAlreadyMinimalDFA_ReturnsOriginalStateCount()
    {
        // Arrange 
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new State { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            Input = "test"
        };

        // Act
        var (result, message) = service.MinimizeDfa(model);

        // Assert
        result.States.Count.ShouldBe(1);
        message.ShouldContain("already minimal");
        message.ShouldContain("1 states");
        result.Input.ShouldBe("test");
        result.IsCustomAutomaton.ShouldBeTrue();
        result.HasExecuted.ShouldBeFalse();
    }

    [Fact]
    public void MinimizeDfa_WithReducibleDFA_MinimizesCorrectly()
    {
        // Arrange
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new State { Id = 1, IsStart = true, IsAccepting = false },
                new State { Id = 2, IsStart = false, IsAccepting = true },
                new State { Id = 3, IsStart = false, IsAccepting = false }, // Unreachable
            ],
            Transitions =
            [
                new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
                new Transition { FromStateId = 2, ToStateId = 2, Symbol = 'a' },
            ],
            Input = ""
        };

        // Act
        var (result, message) = service.MinimizeDfa(model);

        // Assert
        result.States.Count.ShouldBeLessThan(model.States.Count);
        message.ShouldContain("->");
        message.ShouldContain($"{model.States.Count}");
        message.ShouldContain($"{result.States.Count}");
        result.Type.ShouldBe(AutomatonType.DFA);
    }

    [Fact]
    public void MinimizeDfa_WithEquivalentStates_MergesThem()
    {
        // Arrange 
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new State { Id = 1, IsStart = true, IsAccepting = false },
                new State { Id = 2, IsStart = false, IsAccepting = true },
                new State { Id = 3, IsStart = false, IsAccepting = true },
            ],
            Transitions =
            [
                new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
                new Transition { FromStateId = 1, ToStateId = 3, Symbol = 'b' },
                new Transition { FromStateId = 2, ToStateId = 2, Symbol = 'a' },
                new Transition { FromStateId = 2, ToStateId = 2, Symbol = 'b' },
                new Transition { FromStateId = 3, ToStateId = 3, Symbol = 'a' },
                new Transition { FromStateId = 3, ToStateId = 3, Symbol = 'b' },
            ]
        };

        // Act
        var (result, _) = service.MinimizeDfa(model);

        // Assert
        result.States.Count.ShouldBe(2);
        result.StateMapping.ShouldNotBeNull();
        result.MergedStateGroups.ShouldNotBeNull();
        result.MinimizationReport.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void MinimizeDfa_PreservesSourceRegex()
    {
        // Arrange
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new State { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            SourceRegex = "a*"
        };

        // Act
        var (result, _) = service.MinimizeDfa(model);

        // Assert
        result.SourceRegex.ShouldBe("a*");
    }

    [Fact]
    public void MinimizeDfa_ClearsExecutionState()
    {
        // Arrange
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new State { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            Input = "test",
            HasExecuted = true,
            Position = 2,
            CurrentStateId = 1,
            IsAccepted = true,
            StateHistorySerialized = "[[1],[1]]"
        };

        // Act
        var (result, _) = service.MinimizeDfa(model);

        // Assert
        result.Input.ShouldBe("test"); // Input preserved
        result.HasExecuted.ShouldBeFalse();
        result.Position.ShouldBe(0);
        result.CurrentStateId.ShouldBeNull();
        result.IsAccepted.ShouldBeNull();
        result.StateHistorySerialized.ShouldBe(string.Empty);
    }

    [Fact]
    public void MinimizeDfa_WithEmptyDFA_ThrowsInvalidOperationException()
    {
        // Arrange 
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [],
            Transitions = []
        };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => service.MinimizeDfa(model));
    }

    [Fact]
    public void MinimizeDfa_CreatesStateMappingForMergedStates()
    {
        // Arrange 
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new State { Id = 1, IsStart = true, IsAccepting = false },
                new State { Id = 2, IsStart = false, IsAccepting = true },
                new State { Id = 3, IsStart = false, IsAccepting = true },
            ],
            Transitions =
            [
                new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
                new Transition { FromStateId = 1, ToStateId = 3, Symbol = 'b' },
                new Transition { FromStateId = 2, ToStateId = 2, Symbol = 'a' },
                new Transition { FromStateId = 2, ToStateId = 2, Symbol = 'b' },
                new Transition { FromStateId = 3, ToStateId = 3, Symbol = 'a' },
                new Transition { FromStateId = 3, ToStateId = 3, Symbol = 'b' },
            ]
        };

        // Act
        var (result, _) = service.MinimizeDfa(model);

        // Assert
        result.StateMapping.ShouldNotBeNull();
        result.StateMapping.Count.ShouldBe(3);
        result.MergedStateGroups.ShouldNotBeNull();
        result.MergedStateGroups.Values.Sum(g => g.Count).ShouldBe(3);
    }

    [Fact]
    public void MinimizeDfa_GeneratesMinimizationReport()
    {
        // Arrange
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new State { Id = 1, IsStart = true, IsAccepting = false },
                new State { Id = 2, IsStart = false, IsAccepting = true },
                new State { Id = 3, IsStart = false, IsAccepting = true },
            ],
            Transitions =
            [
                new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
                new Transition { FromStateId = 1, ToStateId = 3, Symbol = 'b' },
                new Transition { FromStateId = 2, ToStateId = 2, Symbol = 'a' },
                new Transition { FromStateId = 2, ToStateId = 2, Symbol = 'b' },
                new Transition { FromStateId = 3, ToStateId = 3, Symbol = 'a' },
                new Transition { FromStateId = 3, ToStateId = 3, Symbol = 'b' },
            ]
        };

        // Act
        var (result, _) = service.MinimizeDfa(model);

        // Assert
        result.MinimizationReport.ShouldNotBeNullOrWhiteSpace();
        result.MinimizationReport.ShouldContain("New state");
        result.MinimizationReport.ShouldContain("<-");
    }

    [Fact]
    public void AnalyzeAutomaton_WithNonDFA_ReturnsNotSupported()
    {
        // Arrange
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States = [new State { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = []
        };

        // Act
        var analysis = service.AnalyzeAutomaton(model);

        // Assert
        analysis.SupportsMinimization.ShouldBeFalse();
        analysis.IsMinimal.ShouldBeFalse();
    }

    [Fact]
    public void AnalyzeAutomaton_WithEmptyDFA_ReturnsNotSupported()
    {
        // Arrange
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [],
            Transitions = []
        };

        // Act
        var analysis = service.AnalyzeAutomaton(model);

        // Assert
        analysis.SupportsMinimization.ShouldBeFalse();
        analysis.OriginalStateCount.ShouldBe(0);
    }

    [Fact]
    public void AnalyzeAutomaton_WithNoStartState_ReturnsNotMinimal()
    {
        // Arrange
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new State { Id = 1, IsStart = false, IsAccepting = true }],
            Transitions = []
        };

        // Act
        var analysis = service.AnalyzeAutomaton(model);

        // Assert
        analysis.SupportsMinimization.ShouldBeTrue();
        analysis.IsMinimal.ShouldBeFalse();
        analysis.ReachableStateCount.ShouldBe(0);
    }

    [Fact]
    public void AnalyzeAutomaton_WithMinimalDFA_ReturnsIsMinimalTrue()
    {
        // Arrange
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new State { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = []
        };

        // Act
        var analysis = service.AnalyzeAutomaton(model);

        // Assert
        analysis.SupportsMinimization.ShouldBeTrue();
        analysis.IsMinimal.ShouldBeTrue();
        analysis.OriginalStateCount.ShouldBe(1);
        analysis.ReachableStateCount.ShouldBe(1);
        analysis.MinimizedStateCount.ShouldBe(1);
    }

    [Fact]
    public void AnalyzeAutomaton_WithNonMinimalDFA_ReturnsIsMinimalFalse()
    {
        // Arrange
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new State { Id = 1, IsStart = true, IsAccepting = false },
                new State { Id = 2, IsStart = false, IsAccepting = true },
                new State { Id = 3, IsStart = false, IsAccepting = false }, // Unreachable
            ],
            Transitions =
            [
                new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
                new Transition { FromStateId = 2, ToStateId = 2, Symbol = 'a' },
            ]
        };

        // Act
        var analysis = service.AnalyzeAutomaton(model);

        // Assert
        analysis.SupportsMinimization.ShouldBeTrue();
        analysis.IsMinimal.ShouldBeFalse();
        analysis.OriginalStateCount.ShouldBe(3);
        analysis.ReachableStateCount.ShouldBe(2);
        analysis.MinimizedStateCount.ShouldBeLessThan(analysis.OriginalStateCount);
    }

    [Fact]
    public void AnalyzeAutomaton_WithEquivalentStates_DetectsNonMinimal()
    {
        // Arrange 
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new State { Id = 1, IsStart = true, IsAccepting = false },
                new State { Id = 2, IsStart = false, IsAccepting = true },
                new State { Id = 3, IsStart = false, IsAccepting = true },
            ],
            Transitions =
            [
                new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
                new Transition { FromStateId = 1, ToStateId = 3, Symbol = 'b' },
                new Transition { FromStateId = 2, ToStateId = 2, Symbol = 'a' },
                new Transition { FromStateId = 2, ToStateId = 2, Symbol = 'b' },
                new Transition { FromStateId = 3, ToStateId = 3, Symbol = 'a' },
                new Transition { FromStateId = 3, ToStateId = 3, Symbol = 'b' },
            ]
        };

        // Act
        var analysis = service.AnalyzeAutomaton(model);

        // Assert
        analysis.SupportsMinimization.ShouldBeTrue();
        analysis.IsMinimal.ShouldBeFalse();
        analysis.ReachableStateCount.ShouldBe(3);
        analysis.MinimizedStateCount.ShouldBe(2); // States 2 and 3 should merge
    }

    [Fact]
    public void AnalyzeAutomaton_WithNullCollections_InitializesAndAnalyzes()
    {
        // Arrange
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [],
            Transitions = []
        };

        // Act
        var analysis = service.AnalyzeAutomaton(model);

        // Assert
        analysis.ShouldNotBeNull();
        analysis.SupportsMinimization.ShouldBeFalse();
    }

    [Fact]
    public void AnalyzeAutomaton_ReturnsCorrectStateCounts()
    {
        // Arrange 
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new State { Id = 1, IsStart = true, IsAccepting = false },
                new State { Id = 2, IsStart = false, IsAccepting = true },
            ],
            Transitions =
            [
                new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
                new Transition { FromStateId = 2, ToStateId = 1, Symbol = 'a' },
            ]
        };

        // Act
        var analysis = service.AnalyzeAutomaton(model);

        // Assert
        analysis.OriginalStateCount.ShouldBe(2);
        analysis.ReachableStateCount.ShouldBe(2);
        analysis.MinimizedStateCount.ShouldBe(2);
    }

    [Fact]
    public void MinimizeDfa_WithComplexDFA_MinimizesCorrectly()
    {
        // Arrange 
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new State { Id = 1, IsStart = true, IsAccepting = true },  // Even 'a's
                new State { Id = 2, IsStart = false, IsAccepting = false }, // Odd 'a's
                new State { Id = 3, IsStart = false, IsAccepting = true },  // Even 'a's (duplicate)
            ],
            Transitions =
            [
                new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
                new Transition { FromStateId = 2, ToStateId = 1, Symbol = 'a' },
                new Transition { FromStateId = 3, ToStateId = 2, Symbol = 'a' },
            ]
        };

        // Act
        var (result, _) = service.MinimizeDfa(model);

        // Assert 
        result.States.Count.ShouldBe(2);
    }

    [Fact]
    public void MinimizeDfa_WithSingleStateLoop_RemainsMinimal()
    {
        // Arrange
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new State { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a' }]
        };

        // Act
        var (result, message) = service.MinimizeDfa(model);

        // Assert
        result.States.Count.ShouldBe(1);
        message.ShouldContain("already minimal");
    }

    [Fact]
    public void AnalyzeAutomaton_WithPDA_ReturnsNotSupported()
    {
        // Arrange
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States = [new State { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = []
        };

        // Act
        var analysis = service.AnalyzeAutomaton(model);

        // Assert
        analysis.SupportsMinimization.ShouldBeFalse();
    }

    [Fact]
    public void AnalyzeAutomaton_WithEpsilonNFA_ReturnsNotSupported()
    {
        // Arrange
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.EpsilonNFA,
            States = [new State { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = []
        };

        // Act
        var analysis = service.AnalyzeAutomaton(model);

        // Assert
        analysis.SupportsMinimization.ShouldBeFalse();
    }
}
