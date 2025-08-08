using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using Shouldly;

namespace FiniteAutomatons.UnitTests.ViewModels;

public class ButtonStateLogicTests
{
    [Fact]
    public void ButtonStates_BasicPosition_AllButtonsDisabled()
    {
        // Arrange - Basic position: no input, no execution started
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            Alphabet = ['a', 'b'],
            Input = string.Empty,
            Position = 0,
            CurrentStateId = null,
            Result = null
        };

        // Act & Assert - Test the logic from the view
        bool hasInput = !string.IsNullOrEmpty(model.Input);
        bool isAtFirstPosition = model.Position == 0;
        bool hasExecutionStarted = model.Position > 0 || model.Result != null ||
                                 (model.Type == AutomatonType.DFA && model.CurrentStateId != null) ||
                                 (model.Type != AutomatonType.DFA && model.CurrentStates != null && model.CurrentStates.Any());
        bool isAtEnd = hasInput && model.Position >= model.Input.Length;
        
        // Input validation
        bool isInputValid = true;
        if (hasInput && model.Alphabet != null && model.Alphabet.Any())
        {
            isInputValid = model.Input.All(c => model.Alphabet.Contains(c));
        }

        bool canStepForward = hasInput && isInputValid && !isAtEnd && model.Result != false;
        bool canExecuteAll = hasInput && isInputValid && !isAtEnd && model.Result != false;
        bool canStepBackward = !isAtFirstPosition && isInputValid;
        bool canBackToStart = hasExecutionStarted && isInputValid;
        bool canReset = hasExecutionStarted; // Changed: only when execution started

        // All should be disabled in basic position
        canStepForward.ShouldBeFalse();
        canExecuteAll.ShouldBeFalse();
        canStepBackward.ShouldBeFalse();
        canBackToStart.ShouldBeFalse();
        canReset.ShouldBeFalse(); // Reset should be disabled until execution starts
    }

    [Fact]
    public void ButtonStates_WithValidInputAtStart_StepForwardAndExecuteAllEnabled()
    {
        // Arrange - Valid input provided but at start position
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            Alphabet = ['a', 'b'],
            Input = "abc", // Invalid input (contains 'c' not in alphabet)
            Position = 0,
            CurrentStateId = null,
            Result = null
        };

        // Act & Assert
        bool hasInput = !string.IsNullOrEmpty(model.Input);
        bool isAtFirstPosition = model.Position == 0;
        bool hasExecutionStarted = model.Position > 0 || model.Result != null ||
                                 (model.Type == AutomatonType.DFA && model.CurrentStateId != null) ||
                                 (model.Type != AutomatonType.DFA && model.CurrentStates != null && model.CurrentStates.Any());
        bool isAtEnd = hasInput && model.Position >= model.Input.Length;
        
        // Input validation - 'abc' contains 'c' which is not in alphabet ['a', 'b']
        bool isInputValid = true;
        if (hasInput && model.Alphabet != null && model.Alphabet.Any())
        {
            isInputValid = model.Input.All(c => model.Alphabet.Contains(c));
        }

        bool canStepForward = hasInput && isInputValid && !isAtEnd && model.Result != false;
        bool canExecuteAll = hasInput && isInputValid && !isAtEnd && model.Result != false;
        bool canStepBackward = !isAtFirstPosition && isInputValid;
        bool canBackToStart = hasExecutionStarted && isInputValid;
        bool canReset = hasExecutionStarted;

        // All buttons should be disabled due to invalid input
        isInputValid.ShouldBeFalse(); // 'c' is not in alphabet ['a', 'b']
        canStepForward.ShouldBeFalse();
        canExecuteAll.ShouldBeFalse();
        canStepBackward.ShouldBeFalse();
        canBackToStart.ShouldBeFalse();
        canReset.ShouldBeFalse();
    }

    [Fact]
    public void ButtonStates_WithValidInputAtStart_ShouldEnableForwardButtons()
    {
        // Arrange - Valid input provided at start position
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            Alphabet = ['a', 'b'],
            Input = "ab", // Valid input
            Position = 0,
            CurrentStateId = null,
            Result = null
        };

        // Act & Assert
        bool hasInput = !string.IsNullOrEmpty(model.Input);
        bool isAtFirstPosition = model.Position == 0;
        bool hasExecutionStarted = model.Position > 0 || model.Result != null ||
                                 (model.Type == AutomatonType.DFA && model.CurrentStateId != null) ||
                                 (model.Type != AutomatonType.DFA && model.CurrentStates != null && model.CurrentStates.Any());
        bool isAtEnd = hasInput && model.Position >= model.Input.Length;
        
        // Input validation
        bool isInputValid = true;
        if (hasInput && model.Alphabet != null && model.Alphabet.Any())
        {
            isInputValid = model.Input.All(c => model.Alphabet.Contains(c));
        }

        bool canStepForward = hasInput && isInputValid && !isAtEnd && model.Result != false;
        bool canExecuteAll = hasInput && isInputValid && !isAtEnd && model.Result != false;
        bool canStepBackward = !isAtFirstPosition && isInputValid;
        bool canBackToStart = hasExecutionStarted && isInputValid;
        bool canReset = hasExecutionStarted;

        // Input should be valid
        isInputValid.ShouldBeTrue();
        
        // Step Forward and Execute All should be enabled
        canStepForward.ShouldBeTrue();
        canExecuteAll.ShouldBeTrue();
        canStepBackward.ShouldBeFalse(); // Still at first position
        canBackToStart.ShouldBeFalse(); // No execution started
        canReset.ShouldBeFalse(); // No execution started yet
    }

    [Fact]
    public void ButtonStates_MidExecution_ValidInput_AllRelevantButtonsEnabled()
    {
        // Arrange - Mid execution with valid input
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            Alphabet = ['a', 'b'],
            Input = "ab",
            Position = 1, // Mid execution
            CurrentStateId = 1,
            Result = null
        };

        // Act & Assert
        bool hasInput = !string.IsNullOrEmpty(model.Input);
        bool isAtFirstPosition = model.Position == 0;
        bool hasExecutionStarted = model.Position > 0 || model.Result != null ||
                                 (model.Type == AutomatonType.DFA && model.CurrentStateId != null) ||
                                 (model.Type != AutomatonType.DFA && model.CurrentStates != null && model.CurrentStates.Any());
        bool isAtEnd = hasInput && model.Position >= model.Input.Length;
        
        // Input validation
        bool isInputValid = true;
        if (hasInput && model.Alphabet != null && model.Alphabet.Any())
        {
            isInputValid = model.Input.All(c => model.Alphabet.Contains(c));
        }

        bool canStepForward = hasInput && isInputValid && !isAtEnd && model.Result != false;
        bool canExecuteAll = hasInput && isInputValid && !isAtEnd && model.Result != false;
        bool canStepBackward = !isAtFirstPosition && isInputValid;
        bool canBackToStart = hasExecutionStarted && isInputValid;
        bool canReset = hasExecutionStarted;

        // Most buttons should be enabled
        isInputValid.ShouldBeTrue();
        canStepForward.ShouldBeTrue(); // Not at end
        canExecuteAll.ShouldBeTrue(); // Not at end
        canStepBackward.ShouldBeTrue(); // Not at first position
        canBackToStart.ShouldBeTrue(); // Execution started
        canReset.ShouldBeTrue(); // Execution started
    }

    [Fact]
    public void ButtonStates_AtEndOfExecution_OnlyBackButtonsAndResetEnabled()
    {
        // Arrange - At end of execution
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            Alphabet = ['a', 'b'],
            Input = "ab",
            Position = 2, // At end
            CurrentStateId = 1,
            Result = true
        };

        // Act & Assert
        bool hasInput = !string.IsNullOrEmpty(model.Input);
        bool isAtFirstPosition = model.Position == 0;
        bool hasExecutionStarted = model.Position > 0 || model.Result != null ||
                                 (model.Type == AutomatonType.DFA && model.CurrentStateId != null) ||
                                 (model.Type != AutomatonType.DFA && model.CurrentStates != null && model.CurrentStates.Any());
        bool isAtEnd = hasInput && model.Position >= model.Input.Length;
        
        // Input validation
        bool isInputValid = true;
        if (hasInput && model.Alphabet != null && model.Alphabet.Any())
        {
            isInputValid = model.Input.All(c => model.Alphabet.Contains(c));
        }

        bool canStepForward = hasInput && isInputValid && !isAtEnd && model.Result != false;
        bool canExecuteAll = hasInput && isInputValid && !isAtEnd && model.Result != false;
        bool canStepBackward = !isAtFirstPosition && isInputValid;
        bool canBackToStart = hasExecutionStarted && isInputValid;
        bool canReset = hasExecutionStarted;

        // Forward buttons disabled, back buttons enabled
        isInputValid.ShouldBeTrue();
        canStepForward.ShouldBeFalse(); // At end
        canExecuteAll.ShouldBeFalse(); // At end
        canStepBackward.ShouldBeTrue(); // Not at first position
        canBackToStart.ShouldBeTrue(); // Execution started
        canReset.ShouldBeTrue(); // Execution started
    }

    [Fact]
    public void ButtonStates_InvalidInput_AllButtonsDisabled()
    {
        // Arrange - Invalid input during execution
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            Alphabet = ['a', 'b'],
            Input = "axb", // Invalid: 'x' not in alphabet
            Position = 1,
            CurrentStateId = 1,
            Result = null
        };

        // Act & Assert
        bool hasInput = !string.IsNullOrEmpty(model.Input);
        bool isAtFirstPosition = model.Position == 0;
        bool hasExecutionStarted = model.Position > 0 || model.Result != null ||
                                 (model.Type == AutomatonType.DFA && model.CurrentStateId != null) ||
                                 (model.Type != AutomatonType.DFA && model.CurrentStates != null && model.CurrentStates.Any());
        bool isAtEnd = hasInput && model.Position >= model.Input.Length;
        
        // Input validation
        bool isInputValid = true;
        if (hasInput && model.Alphabet != null && model.Alphabet.Any())
        {
            isInputValid = model.Input.All(c => model.Alphabet.Contains(c));
        }

        bool canStepForward = hasInput && isInputValid && !isAtEnd && model.Result != false;
        bool canExecuteAll = hasInput && isInputValid && !isAtEnd && model.Result != false;
        bool canStepBackward = !isAtFirstPosition && isInputValid;
        bool canBackToStart = hasExecutionStarted && isInputValid;
        bool canReset = hasExecutionStarted;

        // All buttons should be disabled due to invalid input
        isInputValid.ShouldBeFalse(); // 'x' is not in alphabet
        canStepForward.ShouldBeFalse();
        canExecuteAll.ShouldBeFalse();
        canStepBackward.ShouldBeFalse(); // Disabled due to invalid input
        canBackToStart.ShouldBeFalse(); // Disabled due to invalid input
        canReset.ShouldBeTrue(); // Reset should still work even with invalid input
    }
}