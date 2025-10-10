using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;

namespace FiniteAutomatons.Services.Services;

public class AutomatonExecutionService(IAutomatonBuilderService builderService, ILogger<AutomatonExecutionService> logger) : IAutomatonExecutionService
{
    private readonly IAutomatonBuilderService builderService = builderService;
    private readonly ILogger<AutomatonExecutionService> logger = logger;

    public AutomatonExecutionState ReconstructState(AutomatonViewModel model)
    {
        model.States ??= [];
        model.Transitions ??= [];

        AutomatonExecutionState state;

        if (model.Type == AutomatonType.DFA || model.Type == AutomatonType.PDA)
        {
            if (model.Type == AutomatonType.PDA)
            {
                var pdaState = new PDAExecutionState(model.Input ?? string.Empty, model.CurrentStateId)
                {
                    Position = model.Position,
                    IsAccepted = model.IsAccepted
                };
                // Deserialize stack
                if (!string.IsNullOrEmpty(model.StackSerialized))
                {
                    try
                    {
                        var stackArray = JsonSerializer.Deserialize<List<char>>(model.StackSerialized) ?? [];
                        pdaState.Stack = new Stack<char>(stackArray.Reverse<char>()); // incoming top-first; Stack enumerates top-first, so reverse to push bottom-first
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to deserialize PDA stack; initializing empty.");
                    }
                }
                // Deserialize PDA history if present (list of snapshots)
                if (!string.IsNullOrEmpty(model.StateHistorySerialized))
                {
                    try
                    {
                        var snaps = JsonSerializer.Deserialize<List<PDAExecutionState.Snapshot>>(model.StateHistorySerialized) ?? [];
                        for (int i = snaps.Count - 1; i >= 0; i--)
                        {
                            pdaState.History.Push(snaps[i]);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to deserialize PDA history");
                    }
                }
                state = pdaState;
            }
            else
            {
                state = new AutomatonExecutionState(model.Input ?? "", model.CurrentStateId)
                {
                    Position = model.Position,
                    IsAccepted = model.IsAccepted
                };
            }
        }
        else
        {
            state = new AutomatonExecutionState(model.Input ?? "", null, model.CurrentStates ?? [])
            {
                Position = model.Position,
                IsAccepted = model.IsAccepted
            };
        }

        if (model.Type != AutomatonType.PDA && !string.IsNullOrEmpty(model.StateHistorySerialized))
        {
            try
            {
                if (model.Type == AutomatonType.DFA)
                {
                    var stackList = JsonSerializer.Deserialize<List<List<int>>>(model.StateHistorySerialized) ?? [];
                    for (int i = stackList.Count - 1; i >= 0; i--)
                    {
                        state.StateHistory.Push([.. stackList[i]]);
                    }
                }
                else
                {
                    var stackList = JsonSerializer.Deserialize<List<HashSet<int>>>(model.StateHistorySerialized) ?? [];
                    for (int i = stackList.Count - 1; i >= 0; i--)
                    {
                        state.StateHistory.Push([.. stackList[i]]);
                    }
                }
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Failed to deserialize state history, starting with empty history");
                state.StateHistory.Clear();
            }
        }

        return state;
    }

    public void UpdateModelFromState(AutomatonViewModel model, AutomatonExecutionState state)
    {
        model.Position = state.Position;
        model.IsAccepted = state.IsAccepted;

        if (model.Type == AutomatonType.DFA || model.Type == AutomatonType.PDA)
        {
            model.CurrentStateId = state.CurrentStateId;
            model.CurrentStates = null;
        }
        else
        {
            model.CurrentStateId = null;
            model.CurrentStates = state.CurrentStates ?? [];
        }

        if (model.Type == AutomatonType.PDA && state is PDAExecutionState pdaState)
        {
            // Serialize stack (top-first)
            model.StackSerialized = JsonSerializer.Serialize(pdaState.Stack.ToArray());
            // Serialize history snapshots
            model.StateHistorySerialized = JsonSerializer.Serialize(pdaState.History.ToArray());
        }
        else
        {
            var stackArray = state.StateHistory.ToArray();
            var stackList = stackArray.Select(s => s.ToList()).ToList();
            model.StateHistorySerialized = JsonSerializer.Serialize(stackList);
        }
    }

    public void EnsureProperStateInitialization(AutomatonViewModel model, Automaton automaton)
    {
        if (model.Type == AutomatonType.DFA || model.Type == AutomatonType.PDA)
        {
            model.CurrentStateId ??= model.States?.FirstOrDefault(s => s.IsStart)?.Id;
            model.CurrentStates = null;
        }
        else
        {
            if (model.CurrentStates == null || model.CurrentStates.Count == 0)
            {
                var startState = model.States?.FirstOrDefault(s => s.IsStart);
                if (startState != null)
                {
                    try
                    {
                        var tempState = automaton.StartExecution("");
                        model.CurrentStates = tempState.CurrentStates ?? [startState.Id];
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to initialize state using StartExecution, using fallback");
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

    public AutomatonViewModel ExecuteStepForward(AutomatonViewModel model)
    {
        model.States ??= [];
        model.Transitions ??= [];
        model.Input ??= "";

        var automaton = builderService.CreateAutomatonFromModel(model);
        EnsureProperStateInitialization(model, automaton);
        var execState = ReconstructState(model);
        automaton.StepForward(execState);
        UpdateModelFromState(model, execState);
        model.Result = execState.IsAccepted;

        logger.LogInformation("Executed step forward, position: {Position}, accepted: {IsAccepted}", 
            model.Position, model.IsAccepted);

        return model;
    }

    public AutomatonViewModel ExecuteStepBackward(AutomatonViewModel model)
    {
        model.States ??= [];
        model.Transitions ??= [];
        model.Input ??= "";

        var automaton = builderService.CreateAutomatonFromModel(model);
        var execState = ReconstructState(model);
        automaton.StepBackward(execState);
        UpdateModelFromState(model, execState);
        model.Result = execState.IsAccepted;

        logger.LogInformation("Executed step backward, position: {Position}, accepted: {IsAccepted}", 
            model.Position, model.IsAccepted);

        return model;
    }

    public AutomatonViewModel ExecuteAll(AutomatonViewModel model)
    {
        model.States ??= [];
        model.Transitions ??= [];
        model.Input ??= "";

        var automaton = builderService.CreateAutomatonFromModel(model);
        EnsureProperStateInitialization(model, automaton);
        var execState = ReconstructState(model);
        automaton.ExecuteAll(execState);
        UpdateModelFromState(model, execState);
        model.Result = execState.IsAccepted;

        logger.LogInformation("Executed all steps, final position: {Position}, accepted: {IsAccepted}", 
            model.Position, model.IsAccepted);

        return model;
    }

    public AutomatonViewModel BackToStart(AutomatonViewModel model)
    {
        model.States ??= [];
        model.Transitions ??= [];
        model.Input ??= "";

        var automaton = builderService.CreateAutomatonFromModel(model);
        var execState = automaton.StartExecution(model.Input);
        UpdateModelFromState(model, execState);
        model.Result = execState.IsAccepted;

        logger.LogInformation("Reset to start state, position: {Position}", model.Position);

        return model;
    }

    public AutomatonViewModel ResetExecution(AutomatonViewModel model)
    {
        model.States ??= [];
        model.Transitions ??= [];

        model.Input = string.Empty;
        model.Result = null;
        model.CurrentStateId = null;
        model.CurrentStates = null;
        model.Position = 0;
        model.IsAccepted = null;
        model.StateHistorySerialized = string.Empty;
        model.StackSerialized = null;

        logger.LogInformation("Reset execution state while preserving automaton structure");

        return model;
    }
}