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
        model.HasExecuted = true;
        var updated = executionService.BackToStart(model);
        updated.HasExecuted = true;
        ModelState.Clear(); // ensure updated values rendered
        StoreMinimizationAnalysis(updated);
        return View("../Home/Index", updated);
    }

    [HttpPost]
    public IActionResult StepForward([FromForm] AutomatonViewModel model)
    {
        model.HasExecuted = true;
        var updated = executionService.ExecuteStepForward(model);
        updated.HasExecuted = true;
        ModelState.Clear(); // ensure updated values rendered
        StoreMinimizationAnalysis(updated);
        return View("../Home/Index", updated);
    }

    [HttpPost]
    public IActionResult StepBackward([FromForm] AutomatonViewModel model)
    {
        var updated = executionService.ExecuteStepBackward(model);
        updated.HasExecuted = model.HasExecuted || updated.Position > 0;
        ModelState.Clear(); // ensure updated values rendered
        StoreMinimizationAnalysis(updated);
        return View("../Home/Index", updated);
    }

    [HttpPost]
    public IActionResult ExecuteAll([FromForm] AutomatonViewModel model)
    {
        model.HasExecuted = true;
        var updated = executionService.ExecuteAll(model);
        updated.HasExecuted = true;
        ModelState.Clear(); // ensure updated values rendered
        StoreMinimizationAnalysis(updated);
        return View("../Home/Index", updated);
    }

    [HttpPost]
    public IActionResult BackToStart([FromForm] AutomatonViewModel model)
    {
        var updated = executionService.BackToStart(model);
        updated.HasExecuted = model.HasExecuted || model.Position > 0 || model.Result != null;
        ModelState.Clear(); // ensure updated values rendered
        StoreMinimizationAnalysis(updated);
        return View("../Home/Index", updated);
    }

    [HttpPost]
    public IActionResult Reset([FromForm] AutomatonViewModel model)
    {
        var updated = executionService.ResetExecution(model);
        updated.HasExecuted = false;
        ModelState.Clear(); // ensure updated values rendered
        StoreMinimizationAnalysis(updated);
        return View("../Home/Index", updated);
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
}
