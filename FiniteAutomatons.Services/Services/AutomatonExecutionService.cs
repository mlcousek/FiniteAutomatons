using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

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

        if (model.Type == AutomatonType.DPDA)
        {
            var dpdaState = new PDAExecutionState(model.Input ?? string.Empty, model.CurrentStateId)
            {
                Position = model.Position,
                IsAccepted = model.IsAccepted
            };
            if (!string.IsNullOrEmpty(model.StackSerialized))
            {
                try
                {
                    var stackArray = JsonSerializer.Deserialize<List<char>>(model.StackSerialized) ?? [];
                    dpdaState.Stack = new Stack<char>(stackArray.AsEnumerable().Reverse());
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to deserialize DPDA stack; initializing empty.");
                }
            }
            else
            {
                dpdaState.Stack = new Stack<char>();
                dpdaState.Stack.Push('#');
            }
            if (!string.IsNullOrEmpty(model.StateHistorySerialized))
            {
                try
                {
                    var snaps = JsonSerializer.Deserialize<List<PDAExecutionState.Snapshot>>(model.StateHistorySerialized) ?? [];
                    for (int i = snaps.Count - 1; i >= 0; i--)
                    {
                        dpdaState.History.Push(snaps[i]);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to deserialize DPDA history");
                }
            }
            state = dpdaState;
        }
        else if (model.Type == AutomatonType.NPDA)
        {
            var npdaState = new NPDAExecutionState(model.Input ?? string.Empty, model.States?.FirstOrDefault(s => s.IsStart)?.Id ?? 0, '#')
            {
                Position = model.Position,
                IsAccepted = model.IsAccepted
            };

            if (!string.IsNullOrEmpty(model.NPDAConfigurationsSerialized))
            {
                try
                {
                    var configDtos = JsonSerializer.Deserialize<List<PDAConfigurationDto>>(model.NPDAConfigurationsSerialized) ?? [];
                    var configs = new HashSet<PDAConfiguration>();
                    foreach (var dto in configDtos)
                    {
                        var stack = System.Collections.Immutable.ImmutableStack<char>.Empty;
                        if (dto.Stack != null)
                        {
                            foreach (char c in dto.Stack)
                                stack = stack.Push(c);
                        }
                        configs.Add(new PDAConfiguration(dto.StateId, stack));
                    }
                    npdaState.Configurations = configs;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to deserialize NPDA configurations");
                }
            }

            if (!string.IsNullOrEmpty(model.StateHistorySerialized))
            {
                try
                {
                    var historyDtos = JsonSerializer.Deserialize<List<List<PDAConfigurationDto>>>(model.StateHistorySerialized) ?? [];
                    for (int i = historyDtos.Count - 1; i >= 0; i--)
                    {
                        var configs = new HashSet<PDAConfiguration>();
                        foreach (var dto in historyDtos[i])
                        {
                            var stack = System.Collections.Immutable.ImmutableStack<char>.Empty;
                            if (dto.Stack != null)
                            {
                                foreach (char c in dto.Stack)
                                    stack = stack.Push(c);
                            }
                            configs.Add(new PDAConfiguration(dto.StateId, stack));
                        }
                        npdaState.History.Push(configs);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to deserialize NPDA history");
                }
            }
            state = npdaState;
        }
        else if (model.Type == AutomatonType.DFA)
        {
            state = new AutomatonExecutionState(model.Input ?? "", model.CurrentStateId)
            {
                Position = model.Position,
                IsAccepted = model.IsAccepted
            };
        }
        else
        {
            state = new AutomatonExecutionState(model.Input ?? "", null, model.CurrentStates ?? [])
            {
                Position = model.Position,
                IsAccepted = model.IsAccepted
            };
        }

        if (model.Type != AutomatonType.DPDA && model.Type != AutomatonType.NPDA && !string.IsNullOrEmpty(model.StateHistorySerialized))
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

        if (model.Type == AutomatonType.DFA || model.Type == AutomatonType.DPDA)
        {
            model.CurrentStateId = state.CurrentStateId;
            model.CurrentStates = null;
        }
        else if (model.Type == AutomatonType.NPDA)
        {
            model.CurrentStateId = null;
            model.CurrentStates = null;
        }
        else
        {
            model.CurrentStateId = null;
            model.CurrentStates = state.CurrentStates ?? [];
        }

        if (model.Type == AutomatonType.DPDA && state is PDAExecutionState dpdaState)
        {
            model.StackSerialized = JsonSerializer.Serialize(dpdaState.Stack.ToArray());
            model.StateHistorySerialized = JsonSerializer.Serialize(dpdaState.History.ToArray());
        }
        else if (model.Type == AutomatonType.NPDA && state is NPDAExecutionState npdaState)
        {
            var configDtos = npdaState.Configurations.Select(c => new PDAConfigurationDto { StateId = c.StateId, Stack = SerializeImmutableStack(c.Stack) }).ToList();
            model.NPDAConfigurationsSerialized = JsonSerializer.Serialize(configDtos);

            var historyDtos = npdaState.History.Select(hs =>
                hs.Select(c => new PDAConfigurationDto { StateId = c.StateId, Stack = SerializeImmutableStack(c.Stack) }).ToList()
            ).ToList();
            model.StateHistorySerialized = JsonSerializer.Serialize(historyDtos);
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
        if (model.Type == AutomatonType.DFA || model.Type == AutomatonType.DPDA)
        {
            model.CurrentStateId ??= model.States?.FirstOrDefault(s => s.IsStart)?.Id;
            model.CurrentStates = null;
        }
        else if (model.Type == AutomatonType.NPDA)
        {
            if (string.IsNullOrEmpty(model.NPDAConfigurationsSerialized))
            {
                var startState = model.States?.FirstOrDefault(s => s.IsStart);
                if (startState != null)
                {
                    try
                    {
                        var tempState = (NPDAExecutionState)automaton.StartExecution("");
                        var configDtos = tempState.Configurations.Select(c => new PDAConfigurationDto { StateId = c.StateId, Stack = SerializeImmutableStack(c.Stack) }).ToList();
                        model.NPDAConfigurationsSerialized = JsonSerializer.Serialize(configDtos);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to initialize NPDA state");
                    }
                }
            }
            model.CurrentStateId = null;
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
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Executed step forward, position: {Position}, accepted: {IsAccepted}",
            model.Position, model.IsAccepted);
        }
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
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Executed step backward, position: {Position}, accepted: {IsAccepted}",
            model.Position, model.IsAccepted);
        }
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
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Executed all steps, final position: {Position}, accepted: {IsAccepted}",
            model.Position, model.IsAccepted);
        }
        return model;
    }

    public AutomatonViewModel BackToStart(AutomatonViewModel model)
    {
        model.States ??= [];
        model.Transitions ??= [];
        model.Input ??= "";

        var automaton = builderService.CreateAutomatonFromModel(model);

        Stack<char>? initialStack = null;
        if ((model.Type == AutomatonType.DPDA || model.Type == AutomatonType.NPDA) && !string.IsNullOrEmpty(model.InitialStackSerialized))
        {
            try
            {
                var stackArray = JsonSerializer.Deserialize<List<char>>(model.InitialStackSerialized) ?? [];
                initialStack = new Stack<char>(stackArray.AsEnumerable().Reverse());
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to deserialize initial stack; using default.");
            }
        }

        var execState = automaton.StartExecution(model.Input, initialStack);
        UpdateModelFromState(model, execState);
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Reset to start state, position: {Position}", model.Position);
        }
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
        model.InitialStackSerialized = null;
        model.NPDAConfigurationsSerialized = null;

        logger.LogInformation("Reset execution state while preserving automaton structure");

        return model;
    }

    private class PDAConfigurationDto
    {
        public int StateId { get; set; }
        public char[] Stack { get; set; } = [];
    }

    private static char[] SerializeImmutableStack(System.Collections.Immutable.ImmutableStack<char> stack)
    {
        return [.. stack.Reverse()];
    }
}