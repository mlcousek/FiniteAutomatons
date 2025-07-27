using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.ViewModel;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace FiniteAutomatons.Controllers;

public class AutomatonController(ILogger<AutomatonController> logger) : Controller
{
    private readonly ILogger<AutomatonController> logger = logger;

    private static AutomatonExecutionState ReconstructState(DfaViewModel model)
    {
        var state = new AutomatonExecutionState(model.Input, model.CurrentStateId)
        {
            Position = model.Position,
            IsAccepted = model.IsAccepted
        };
        // Deserialize StateHistory if present
        if (!string.IsNullOrEmpty(model.StateHistorySerialized))
        {
            var stackList = JsonSerializer.Deserialize<List<List<int>>>(model.StateHistorySerialized) ?? [];
            // Since we serialized as [top, ..., bottom], we need to push in reverse order
            // to restore the original stack order
            for (int i = stackList.Count - 1; i >= 0; i--)
            {
                state.StateHistory.Push([.. stackList[i]]);
            }
        }
        return state;
    }

    private static void UpdateModelFromState(DfaViewModel model, AutomatonExecutionState state)
    {
        model.CurrentStateId = state.CurrentStateId;
        model.Position = state.Position;
        model.IsAccepted = state.IsAccepted;
        // Serialize StateHistory - convert stack to array first to preserve LIFO order
        var stackArray = state.StateHistory.ToArray(); // This gives us [top, ..., bottom]
        var stackList = stackArray.Select(s => s.ToList()).ToList();
        model.StateHistorySerialized = JsonSerializer.Serialize(stackList);
    }

    private static void EnsureNotNullCurrentStateId(DfaViewModel model)
    {
        model.CurrentStateId ??= model.States.FirstOrDefault(s => s.IsStart)?.Id;
    }

    [HttpPost]
    public IActionResult StepForward([FromForm] DfaViewModel model)
    {
        var dfa = new DFA();
        dfa.States.AddRange(model.States);
        dfa.Transitions.AddRange(model.Transitions);
        EnsureNotNullCurrentStateId(model);
        var execState = ReconstructState(model);
        dfa.StepForward(execState);
        UpdateModelFromState(model, execState);
        model.Result = execState.IsAccepted;
        model.Alphabet = [.. dfa.Transitions.Select(t => t.Symbol).Distinct()];
        return View("../Home/Index", model);
    }

    //TODO when wants to go back but is on start give feedback
    [HttpPost]
    public IActionResult StepBackward([FromForm] DfaViewModel model)
    {
        var dfa = new DFA();
        dfa.States.AddRange(model.States);
        dfa.Transitions.AddRange(model.Transitions);
        var execState = ReconstructState(model);
        dfa.StepBackward(execState);
        UpdateModelFromState(model, execState);
        model.Result = execState.IsAccepted;
        model.Alphabet = [.. dfa.Transitions.Select(t => t.Symbol).Distinct()];
        return View("../Home/Index", model);
    }

    [HttpPost]
    public IActionResult ExecuteAll([FromForm] DfaViewModel model)
    {
        var dfa = new DFA();
        dfa.States.AddRange(model.States);
        dfa.Transitions.AddRange(model.Transitions);
        EnsureNotNullCurrentStateId(model);
        var execState = ReconstructState(model);
        dfa.ExecuteAll(execState);
        UpdateModelFromState(model, execState);
        model.Result = execState.IsAccepted;
        model.Alphabet = [.. dfa.Transitions.Select(t => t.Symbol).Distinct()];
        return View("../Home/Index", model);
    }

    [HttpPost]
    public IActionResult BackToStart([FromForm] DfaViewModel model)
    {
        var dfa = new DFA();
        dfa.States.AddRange(model.States);
        dfa.Transitions.AddRange(model.Transitions);
        var execState = new AutomatonExecutionState(model.Input, dfa.States.FirstOrDefault(s => s.IsStart)?.Id);
        dfa.BackToStart(execState);
        UpdateModelFromState(model, execState);
        model.Result = execState.IsAccepted;
        model.Alphabet = [.. dfa.Transitions.Select(t => t.Symbol).Distinct()];
        return View("../Home/Index", model);
    }

    [HttpPost]
    public IActionResult Reset([FromForm] DfaViewModel model)
    {
        model.Input = string.Empty;
        model.Result = null;
        model.CurrentStateId = null;
        model.Position = 0;
        model.IsAccepted = null;
        model.StateHistorySerialized = string.Empty;

        return View("../Home/Index", model);
    }
}
