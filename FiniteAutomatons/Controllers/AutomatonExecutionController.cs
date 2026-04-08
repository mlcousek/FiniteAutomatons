using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FiniteAutomatons.Controllers;

public class AutomatonExecutionController(IAutomatonTempDataService tempDataService, IAutomatonExecutionService executionService, IAutomatonMinimizationService minimizationService) : Controller
{
    private readonly IAutomatonTempDataService tempDataService = tempDataService;
    private readonly IAutomatonExecutionService executionService = executionService;
    private readonly IAutomatonMinimizationService minimizationService = minimizationService;

    [HttpPost]
    public IActionResult Start([FromForm] AutomatonViewModel model)
    {
        return ExecuteWithHandledDeterminismError(model, m =>
        {
            var updated = executionService.BackToStart(m);
            updated.HasExecuted = true;
            return updated;
        });
    }

    [HttpPost]
    public IActionResult StepForward([FromForm] AutomatonViewModel model)
    {
        return ExecuteWithHandledDeterminismError(model, m =>
        {
            var updated = executionService.ExecuteStepForward(m);
            updated.HasExecuted = true;
            return updated;
        });
    }

    [HttpPost]
    public IActionResult StepBackward([FromForm] AutomatonViewModel model)
    {
        return ExecuteWithHandledDeterminismError(model, m =>
        {
            var updated = executionService.ExecuteStepBackward(m);
            updated.HasExecuted = m.HasExecuted || updated.Position > 0;
            return updated;
        });
    }

    [HttpPost]
    public IActionResult ExecuteAll([FromForm] AutomatonViewModel model)
    {
        return ExecuteWithHandledDeterminismError(model, m =>
        {
            var updated = executionService.ExecuteAll(m);
            updated.HasExecuted = true;
            return updated;
        });
    }

    [HttpPost]
    public IActionResult BackToStart([FromForm] AutomatonViewModel model)
    {
        return ExecuteWithHandledDeterminismError(model, m =>
        {
            var updated = executionService.BackToStart(m);
            updated.HasExecuted = m.HasExecuted || m.Position > 0 || m.Result != null;
            return updated;
        });
    }

    [HttpPost]
    public IActionResult Reset([FromForm] AutomatonViewModel model)
    {
        return ExecuteWithHandledDeterminismError(model, m =>
        {
            var updated = executionService.ResetExecution(m);
            updated.HasExecuted = false;
            return updated;
        });
    }

    [HttpPost]
    public IActionResult Minimalize([FromForm] AutomatonViewModel model)
    {
        // Allow only before execution has started (HasExecuted false and Position == 0)
        if (model.Type != AutomatonType.DFA)
        {
            tempDataService.StoreErrorMessage(TempData, "Minimization supported only for DFA.");
            tempDataService.StoreCustomAutomaton(TempData, model);
            return RedirectToAction("Index", "Home");
        }
        if (model.HasExecuted || model.Position > 0)
        {
            tempDataService.StoreErrorMessage(TempData, "Cannot minimalize after execution has started. Reset or Back to start first.");
            tempDataService.StoreCustomAutomaton(TempData, model);
            return RedirectToAction("Index", "Home");
        }
        var (result, msg) = minimizationService.MinimizeDfa(model);
        tempDataService.StoreCustomAutomaton(TempData, result);
        tempDataService.StoreConversionMessage(TempData, msg);
        StoreMinimizationAnalysis(result);
        return RedirectToAction("Index", "Home");
    }

    // Helper to stash analysis in TempData for Index view
    private void StoreMinimizationAnalysis(AutomatonViewModel model)
    {
        try
        {
            var analysis = minimizationService.AnalyzeAutomaton(model);
            TempData["MinimizationAnalysis"] = System.Text.Json.JsonSerializer.Serialize(analysis);
        }
        catch { /* ignore */ }
    }

    private IActionResult ProcessExecutionResult(AutomatonViewModel model)
    {
        ModelState.Clear();
        StoreMinimizationAnalysis(model);

        if (TempData == null)
        {
            return View("../Home/Index", model);
        }

        tempDataService.StoreCustomAutomaton(TempData, model);
        return RedirectToAction("Index", "Home");
    }

    private IActionResult ExecuteWithHandledDeterminismError(
        AutomatonViewModel model,
        Func<AutomatonViewModel, AutomatonViewModel> operation)
    {
        try
        {
            return ProcessExecutionResult(operation(model));
        }
        catch (InvalidOperationException ex) when (model.Type == AutomatonType.DPDA &&
            ex.Message.Contains("DPDA determinism violated", StringComparison.OrdinalIgnoreCase))
        {
            if (TempData == null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Cannot simulate as DPDA because the automaton is nondeterministic. " +
                    "Switch to NPDA or make transitions deterministic.");
                return View("../Home/Index", model);
            }

            tempDataService.StoreErrorMessage(
                TempData,
                "Cannot simulate as DPDA because the automaton is nondeterministic. " +
                "Switch to NPDA or make transitions deterministic.");

            tempDataService.StoreCustomAutomaton(TempData, model);
            return RedirectToAction("Index", "Home");
        }
    }
}
