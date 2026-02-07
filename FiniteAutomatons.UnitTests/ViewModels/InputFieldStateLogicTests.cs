using FiniteAutomatons.Core.Models.ViewModel;
using Shouldly;

namespace FiniteAutomatons.UnitTests.ViewModels;

public class InputFieldStateLogicTests
{
    [Fact]
    public void InputField_BasicPosition_ShouldBeEnabled()
    {
        // Arrange - Basic position: no input, no execution started
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            Input = string.Empty,
            Position = 0,
            CurrentStateId = null,
            Result = null
        };

        // Act & Assert - Test the input field logic from the view
        bool isInputDisabled = model.Position > 0 || model.Result != null ||
                             (model.Type == AutomatonType.DFA && model.CurrentStateId != null) ||
                             (model.Type != AutomatonType.DFA && model.CurrentStates != null && model.CurrentStates.Count != 0);

        isInputDisabled.ShouldBeFalse();
    }

    [Fact]
    public void InputField_ExecutionStarted_ShouldBeDisabled()
    {
        // Arrange - Execution has started (position > 0)
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            Input = "ab",
            Position = 1, // Execution started
            CurrentStateId = 1,
            Result = null
        };

        // Act & Assert
        bool isInputDisabled = model.Position > 0 || model.Result != null ||
                             (model.Type == AutomatonType.DFA && model.CurrentStateId != null) ||
                             (model.Type != AutomatonType.DFA && model.CurrentStates != null && model.CurrentStates.Count != 0);

        isInputDisabled.ShouldBeTrue();
    }

    [Fact]
    public void InputField_ResultPresent_ShouldBeDisabled()
    {
        // Arrange - Execution completed (result present)
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            Input = "ab",
            Position = 2,
            CurrentStateId = 1,
            Result = true // Result present
        };

        // Act & Assert
        bool isInputDisabled = model.Position > 0 || model.Result != null ||
                             (model.Type == AutomatonType.DFA && model.CurrentStateId != null) ||
                             (model.Type != AutomatonType.DFA && model.CurrentStates != null && model.CurrentStates.Count != 0);

        isInputDisabled.ShouldBeTrue();
    }

    [Fact]
    public void InputField_NFA_WithCurrentStates_ShouldBeDisabled()
    {
        // Arrange - NFA with current states set
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            Input = "a",
            Position = 0,
            CurrentStates = [1, 2], // Current states set
            Result = null
        };

        // Act & Assert
        bool isInputDisabled = model.Position > 0 || model.Result != null ||
                             (model.Type == AutomatonType.DFA && model.CurrentStateId != null) ||
                             (model.Type != AutomatonType.DFA && model.CurrentStates != null && model.CurrentStates.Count != 0);

        isInputDisabled.ShouldBeTrue();
    }

    [Fact]
    public void InputField_AfterReset_ShouldBeEnabled()
    {
        // Arrange - After reset (all execution state cleared)
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            Input = string.Empty, // Reset clears input
            Position = 0, // Reset position
            CurrentStateId = null, // Reset state
            Result = null // Reset result
        };

        // Act & Assert
        bool isInputDisabled = model.Position > 0 || model.Result != null ||
                             (model.Type == AutomatonType.DFA && model.CurrentStateId != null) ||
                             (model.Type != AutomatonType.DFA && model.CurrentStates != null && model.CurrentStates.Count != 0);

        isInputDisabled.ShouldBeFalse();
    }
}
