using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.ViewModel;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace FiniteAutomatons.Controllers;

public class AutomatonController(ILogger<AutomatonController> logger) : Controller
{
    private readonly ILogger<AutomatonController> logger = logger;

    private static AutomatonExecutionState ReconstructState(AutomatonViewModel model)
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
            catch (JsonException)
            {
                // If deserialization fails, start with empty history
                state.StateHistory.Clear();
            }
        }
        return state;
    }

    private static void UpdateModelFromState(AutomatonViewModel model, AutomatonExecutionState state)
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

    private static void EnsureProperStateInitialization(AutomatonViewModel model, Automaton automaton)
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
                    catch
                    {
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

    private static Automaton CreateAutomatonFromModel(AutomatonViewModel model)
    {
        // Ensure model has required collections initialized
        model.States ??= [];
        model.Transitions ??= [];
        model.Alphabet ??= [];

        return model.Type switch
        {
            AutomatonType.DFA => CreateDFA(model),
            AutomatonType.NFA => CreateNFA(model),
            AutomatonType.EpsilonNFA => CreateEpsilonNFA(model),
            _ => throw new ArgumentException($"Unsupported automaton type: {model.Type}")
        };
    }

    private static DFA CreateDFA(AutomatonViewModel model)
    {
        var dfa = new DFA();

        // Add states safely
        foreach (var state in model.States ?? [])
        {
            dfa.States.Add(state);
        }

        // Add transitions safely
        foreach (var transition in model.Transitions ?? [])
        {
            dfa.Transitions.Add(transition);
        }

        var startState = model.States?.FirstOrDefault(s => s.IsStart);
        if (startState != null)
        {
            dfa.SetStartState(startState.Id);
        }
        return dfa;
    }

    private static NFA CreateNFA(AutomatonViewModel model)
    {
        var nfa = new NFA();

        // Add states safely
        foreach (var state in model.States ?? [])
        {
            nfa.States.Add(state);
        }

        // Add transitions safely
        foreach (var transition in model.Transitions ?? [])
        {
            nfa.Transitions.Add(transition);
        }

        var startState = model.States?.FirstOrDefault(s => s.IsStart);
        if (startState != null)
        {
            nfa.SetStartState(startState.Id);
        }
        return nfa;
    }

    private static EpsilonNFA CreateEpsilonNFA(AutomatonViewModel model)
    {
        var enfa = new EpsilonNFA();

        // Add states safely
        foreach (var state in model.States ?? [])
        {
            enfa.States.Add(state);
        }

        // Add transitions safely
        foreach (var transition in model.Transitions ?? [])
        {
            enfa.Transitions.Add(transition);
        }

        var startState = model.States?.FirstOrDefault(s => s.IsStart);
        if (startState != null)
        {
            enfa.SetStartState(startState.Id);
        }
        return enfa;
    }

    [HttpPost]
    public IActionResult StepForward([FromForm] AutomatonViewModel model)
    {
        try
        {
            // Ensure collections are initialized
            model.States ??= [];
            model.Transitions ??= [];
            model.Alphabet ??= [];
            model.Input ??= "";

            var automaton = CreateAutomatonFromModel(model);
            EnsureProperStateInitialization(model, automaton);
            var execState = ReconstructState(model);
            automaton.StepForward(execState);
            UpdateModelFromState(model, execState);
            model.Result = execState.IsAccepted;
            model.Alphabet = [.. automaton.Transitions.Select(t => t.Symbol).Where(s => s != '\0').Distinct()];
            return View("../Home/Index", model);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in StepForward");
            TempData["ErrorMessage"] = "An error occurred while stepping forward.";
            return View("../Home/Index", model);
        }
    }

    [HttpPost]
    public IActionResult StepBackward([FromForm] AutomatonViewModel model)
    {
        try
        {
            // Ensure collections are initialized
            model.States ??= [];
            model.Transitions ??= [];
            model.Alphabet ??= [];
            model.Input ??= "";

            var automaton = CreateAutomatonFromModel(model);
            var execState = ReconstructState(model);
            automaton.StepBackward(execState);
            UpdateModelFromState(model, execState);
            model.Result = execState.IsAccepted;
            model.Alphabet = [.. automaton.Transitions.Select(t => t.Symbol).Where(s => s != '\0').Distinct()];
            return View("../Home/Index", model);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in StepBackward");
            TempData["ErrorMessage"] = "An error occurred while stepping backward.";
            return View("../Home/Index", model);
        }
    }

    [HttpPost]
    public IActionResult ExecuteAll([FromForm] AutomatonViewModel model)
    {
        try
        {
            // Ensure collections are initialized
            model.States ??= [];
            model.Transitions ??= [];
            model.Alphabet ??= [];
            model.Input ??= "";

            var automaton = CreateAutomatonFromModel(model);
            EnsureProperStateInitialization(model, automaton);
            var execState = ReconstructState(model);
            automaton.ExecuteAll(execState);
            UpdateModelFromState(model, execState);
            model.Result = execState.IsAccepted;
            model.Alphabet = [.. automaton.Transitions.Select(t => t.Symbol).Where(s => s != '\0').Distinct()];
            return View("../Home/Index", model);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in ExecuteAll");
            TempData["ErrorMessage"] = "An error occurred while executing the automaton.";
            return View("../Home/Index", model);
        }
    }

    [HttpPost]
    public IActionResult BackToStart([FromForm] AutomatonViewModel model)
    {
        try
        {
            // Ensure collections are initialized
            model.States ??= [];
            model.Transitions ??= [];
            model.Alphabet ??= [];
            model.Input ??= "";

            var automaton = CreateAutomatonFromModel(model);
            var execState = automaton.StartExecution(model.Input);
            UpdateModelFromState(model, execState);
            model.Result = execState.IsAccepted;
            model.Alphabet = [.. automaton.Transitions.Select(t => t.Symbol).Where(s => s != '\0').Distinct()];
            return View("../Home/Index", model);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in BackToStart");
            TempData["ErrorMessage"] = "An error occurred while resetting to start.";
            return View("../Home/Index", model);
        }
    }

    [HttpPost]
    public IActionResult Reset([FromForm] AutomatonViewModel model)
    {
        try
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
            
            model.Alphabet = transitionSymbols;

            logger.LogInformation("Reset execution state while preserving automaton structure");
            return View("../Home/Index", model);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in Reset");
            TempData["ErrorMessage"] = "An error occurred while resetting.";
            return View("../Home/Index", model);
        }
    }

    [HttpPost]
    public IActionResult ConvertToDFA([FromForm] AutomatonViewModel model)
    {
        try
        {
            // Ensure collections are initialized
            model.States ??= [];
            model.Transitions ??= [];
            model.Alphabet ??= [];

            if (model.Type == AutomatonType.DFA)
            {
                // Already a DFA
                return View("../Home/Index", model);
            }

            var automaton = CreateAutomatonFromModel(model);
            DFA convertedDFA;

            if (automaton is NFA nfa)
            {
                convertedDFA = nfa.ToDFA();
            }
            else if (automaton is EpsilonNFA enfa)
            {
                // Convert EpsilonNFA -> NFA -> DFA
                var intermediateNFA = enfa.ToNFA();
                convertedDFA = intermediateNFA.ToDFA();
            }
            else
            {
                throw new InvalidOperationException("Cannot convert this automaton type to DFA");
            }

            // Create new model with converted DFA
            var convertedModel = new AutomatonViewModel
            {
                Type = AutomatonType.DFA,
                States = [.. convertedDFA.States],
                Transitions = [.. convertedDFA.Transitions],
                Alphabet = [.. convertedDFA.Transitions.Select(t => t.Symbol).Distinct()],
                Input = model.Input ?? "",
                IsCustomAutomaton = true
            };

            // Store the converted automaton
            var modelJson = System.Text.Json.JsonSerializer.Serialize(convertedModel);
            TempData["CustomAutomaton"] = modelJson;
            TempData["ConversionMessage"] = $"Successfully converted {model.TypeDisplayName} to DFA with {convertedModel.States.Count} states.";

            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in ConvertToDFA");
            TempData["ErrorMessage"] = $"Failed to convert to DFA: {ex.Message}";
            return View("../Home/Index", model);
        }
    }
}
