using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FiniteAutomatons.Services.Services;

/// <summary>
/// Service for handling automaton execution operations
/// </summary>
public class AutomatonExecutionService : IAutomatonExecutionService
{
    private readonly IAutomatonBuilderService _builderService;
    private readonly ILogger<AutomatonExecutionService> _logger;

    public AutomatonExecutionService(IAutomatonBuilderService builderService, ILogger<AutomatonExecutionService> logger)
    {
        _builderService = builderService;
        _logger = logger;
    }

    /// <summary>
    /// Reconstructs execution state from a view model
    /// </summary>
    /// <param name="model">The automaton view model</param>
    /// <returns>The reconstructed execution state</returns>
    public AutomatonExecutionState ReconstructState(AutomatonViewModel model)
    {
        // Ensure model has required collections initialized
        model.States ??= [];
        model.Transitions ??= [];
        model.Alphabet ??= [];

        AutomatonExecutionState state;

        if (model.Type == AutomatonType.DFA)
        {
            state = new AutomatonExecutionState(model.Input ?? "", model.CurrentStateId)
            {
                Position = model.Position,
                IsAccepted = model.IsAccepted
            };
        }
        else
        {
            // For NFA and EpsilonNFA, use CurrentStates
            state = new AutomatonExecutionState(model.Input ?? "", null, model.CurrentStates ?? [])
            {
                Position = model.Position,
                IsAccepted = model.IsAccepted
            };
        }

        // Deserialize StateHistory if present
        if (!string.IsNullOrEmpty(model.StateHistorySerialized))
        {
            try
            {
                if (model.Type == AutomatonType.DFA)
                {
                    // DFA uses List<List<int>> where each inner list has one element
                    var stackList = JsonSerializer.Deserialize<List<List<int>>>(model.StateHistorySerialized) ?? [];
                    for (int i = stackList.Count - 1; i >= 0; i--)
                    {
                        state.StateHistory.Push([.. stackList[i]]);
                    }
                }
                else
                {
                    // NFA/EpsilonNFA uses List<HashSet<int>>
                    var stackList = JsonSerializer.Deserialize<List<HashSet<int>>>(model.StateHistorySerialized) ?? [];
                    for (int i = stackList.Count - 1; i >= 0; i--)
                    {
                        state.StateHistory.Push([.. stackList[i]]);
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize state history, starting with empty history");
                // If deserialization fails, start with empty history
                state.StateHistory.Clear();
            }
        }

        return state;
    }

    /// <summary>
    /// Updates a view model from execution state
    /// </summary>
    /// <param name="model">The automaton view model to update</param>
    /// <param name="state">The current execution state</param>
    public void UpdateModelFromState(AutomatonViewModel model, AutomatonExecutionState state)
    {
        model.Position = state.Position;
        model.IsAccepted = state.IsAccepted;

        if (model.Type == AutomatonType.DFA)
        {
            model.CurrentStateId = state.CurrentStateId;
            model.CurrentStates = null;
        }
        else
        {
            model.CurrentStateId = null;
            model.CurrentStates = state.CurrentStates ?? [];
        }

        // Serialize StateHistory
        var stackArray = state.StateHistory.ToArray(); // This gives us [top, ..., bottom]
        var stackList = stackArray.Select(s => s.ToList()).ToList();
        model.StateHistorySerialized = JsonSerializer.Serialize(stackList);
    }

    /// <summary>
    /// Ensures proper state initialization for an automaton
    /// </summary>
    /// <param name="model">The automaton view model</param>
    /// <param name="automaton">The automaton instance</param>
    public void EnsureProperStateInitialization(AutomatonViewModel model, Automaton automaton)
    {
        // Ensure proper state initialization based on automaton type
        if (model.Type == AutomatonType.DFA)
        {
            model.CurrentStateId ??= model.States?.FirstOrDefault(s => s.IsStart)?.Id;
            model.CurrentStates = null;
        }
        else
        {
            // For NFA and EpsilonNFA, initialize with proper start state closure
            if (model.CurrentStates == null || model.CurrentStates.Count == 0)
            {
                var startState = model.States?.FirstOrDefault(s => s.IsStart);
                if (startState != null)
                {
                    try
                    {
                        // Use StartExecution to properly initialize the state
                        var tempState = automaton.StartExecution("");
                        model.CurrentStates = tempState.CurrentStates ?? [startState.Id];
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to initialize state using StartExecution, using fallback");
                        // Fallback if StartExecution fails
                        model.CurrentStates = [startState.Id];
                    }
                }
                else
                {
                    model.CurrentStates = [];
                }
            }
            model.CurrentStateId = null;
        }
    }

    /// <summary>
    /// Executes a single step forward in the automaton
    /// </summary>
    /// <param name="model">The automaton view model</param>
    /// <returns>The updated model after step execution</returns>
    public AutomatonViewModel ExecuteStepForward(AutomatonViewModel model)
    {
        // Ensure collections are initialized
        model.States ??= [];
        model.Transitions ??= [];
        model.Alphabet ??= [];
        model.Input ??= "";

        var automaton = _builderService.CreateAutomatonFromModel(model);
        EnsureProperStateInitialization(model, automaton);
        var execState = ReconstructState(model);
        automaton.StepForward(execState);
        UpdateModelFromState(model, execState);
        model.Result = execState.IsAccepted;
        model.Alphabet = [.. automaton.Transitions.Select(t => t.Symbol).Where(s => s != '\0').Distinct().OrderBy(c => c)];

        _logger.LogInformation("Executed step forward, position: {Position}, accepted: {IsAccepted}", 
            model.Position, model.IsAccepted);

        return model;
    }

    /// <summary>
    /// Executes a single step backward in the automaton
    /// </summary>
    /// <param name="model">The automaton view model</param>
    /// <returns>The updated model after step execution</returns>
    public AutomatonViewModel ExecuteStepBackward(AutomatonViewModel model)
    {
        // Ensure collections are initialized
        model.States ??= [];
        model.Transitions ??= [];
        model.Alphabet ??= [];
        model.Input ??= "";

        var automaton = _builderService.CreateAutomatonFromModel(model);
        var execState = ReconstructState(model);
        automaton.StepBackward(execState);
        UpdateModelFromState(model, execState);
        model.Result = execState.IsAccepted;
        model.Alphabet = [.. automaton.Transitions.Select(t => t.Symbol).Where(s => s != '\0').Distinct().OrderBy(c => c)];

        _logger.LogInformation("Executed step backward, position: {Position}, accepted: {IsAccepted}", 
            model.Position, model.IsAccepted);

        return model;
    }

    /// <summary>
    /// Executes the automaton to completion
    /// </summary>
    /// <param name="model">The automaton view model</param>
    /// <returns>The updated model after full execution</returns>
    public AutomatonViewModel ExecuteAll(AutomatonViewModel model)
    {
        // Ensure collections are initialized
        model.States ??= [];
        model.Transitions ??= [];
        model.Alphabet ??= [];
        model.Input ??= "";

        var automaton = _builderService.CreateAutomatonFromModel(model);
        EnsureProperStateInitialization(model, automaton);
        var execState = ReconstructState(model);
        automaton.ExecuteAll(execState);
        UpdateModelFromState(model, execState);
        model.Result = execState.IsAccepted;
        model.Alphabet = [.. automaton.Transitions.Select(t => t.Symbol).Where(s => s != '\0').Distinct().OrderBy(c => c)];

        _logger.LogInformation("Executed all steps, final position: {Position}, accepted: {IsAccepted}", 
            model.Position, model.IsAccepted);

        return model;
    }

    /// <summary>
    /// Resets the automaton to the start state
    /// </summary>
    /// <param name="model">The automaton view model</param>
    /// <returns>The updated model after reset</returns>
    public AutomatonViewModel BackToStart(AutomatonViewModel model)
    {
        // Ensure collections are initialized
        model.States ??= [];
        model.Transitions ??= [];
        model.Alphabet ??= [];
        model.Input ??= "";

        var automaton = _builderService.CreateAutomatonFromModel(model);
        var execState = automaton.StartExecution(model.Input);
        UpdateModelFromState(model, execState);
        model.Result = execState.IsAccepted;
        model.Alphabet = [.. automaton.Transitions.Select(t => t.Symbol).Where(s => s != '\0').Distinct().OrderBy(c => c)];

        _logger.LogInformation("Reset to start state, position: {Position}", model.Position);

        return model;
    }

    /// <summary>
    /// Resets the execution state while preserving automaton structure
    /// </summary>
    /// <param name="model">The automaton view model</param>
    /// <returns>The updated model after reset</returns>
    public AutomatonViewModel ResetExecution(AutomatonViewModel model)
    {
        // Ensure collections are initialized
        model.States ??= [];
        model.Transitions ??= [];
        model.Alphabet ??= [];

        // Only reset the execution state and input, NOT the automaton structure
        model.Input = string.Empty;
        model.Result = null;
        model.CurrentStateId = null;
        model.CurrentStates = null;
        model.Position = 0;
        model.IsAccepted = null;
        model.StateHistorySerialized = string.Empty;

        // Preserve the automaton's alphabet - rebuild it from transitions
        var transitionSymbols = model.Transitions
            .Where(t => t.Symbol != '\0') // Exclude epsilon transitions
            .Select(t => t.Symbol)
            .Distinct()
            .ToList();
        
        model.Alphabet = transitionSymbols.OrderBy(c => c).ToList();

        _logger.LogInformation("Reset execution state while preserving automaton structure");

        return model;
    }
}