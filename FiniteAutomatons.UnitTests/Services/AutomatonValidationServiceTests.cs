using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Services;
using Shouldly;

namespace FiniteAutomatons.UnitTests.Services;

public class AutomatonValidationServiceTests
{
    private readonly AutomatonValidationService service;

    public AutomatonValidationServiceTests()
    {
        var logger = new TestLogger<AutomatonValidationService>();
        service = new AutomatonValidationService(logger);
    }

    [Fact]
    public void ValidateAutomaton_ValidDFA_ShouldReturnTrue()
    {
        // Arrange
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }
            ]
        };

        // Act
        var (isValid, errors) = service.ValidateAutomaton(model);

        // Assert
        isValid.ShouldBeTrue();
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void ValidateAutomaton_NoStates_ShouldReturnFalse()
    {
        // Arrange
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [],
            Transitions = []
        };

        // Act
        var (isValid, errors) = service.ValidateAutomaton(model);

        // Assert
        isValid.ShouldBeFalse();
        errors.ShouldContain("Automaton must have at least one state.");
    }

    [Fact]
    public void ValidateAutomaton_NoStartState_ShouldReturnFalse()
    {
        // Arrange
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = false, IsAccepting = true }
            ],
            Transitions = []
        };

        // Act
        var (isValid, errors) = service.ValidateAutomaton(model);

        // Assert
        isValid.ShouldBeFalse();
        errors.ShouldContain("Automaton must have exactly one start state.");
    }

    [Fact]
    public void ValidateStateAddition_DuplicateId_ShouldReturnFalse()
    {
        // Arrange
        var model = new AutomatonViewModel
        {
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false }
            ]
        };

        // Act
        var (isValid, errorMessage) = service.ValidateStateAddition(model, 1, false);

        // Assert
        isValid.ShouldBeFalse();
        errorMessage.ShouldBe("State with ID 1 already exists.");
    }

    [Fact]
    public void ValidateTransitionAddition_ValidTransition_ShouldReturnTrue()
    {
        // Arrange
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions = []
        };

        // Act
        var (isValid, processedSymbol, errorMessage) = service.ValidateTransitionAddition(model, 1, 2, "a");

        // Assert
        isValid.ShouldBeTrue();
        processedSymbol.ShouldBe('a');
        errorMessage.ShouldBeNull();
    }

    [Fact]
    public void ValidateTransitionAddition_EpsilonInDFA_ShouldReturnFalse()
    {
        // Arrange
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions = []
        };

        // Act
        var (isValid, _, errorMessage) = service.ValidateTransitionAddition(model, 1, 2, "ε");

        // Assert
        isValid.ShouldBeFalse();
        errorMessage.ShouldNotBeNull();
        errorMessage.ShouldContain("Epsilon transitions (ε) are only allowed in Epsilon NFAs");
    }
}
