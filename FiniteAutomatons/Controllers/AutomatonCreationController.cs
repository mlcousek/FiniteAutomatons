using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using FiniteAutomatons.Services.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace FiniteAutomatons.Controllers;

public class AutomatonCreationController(
 ILogger<AutomatonCreationController> logger,
 IAutomatonTempDataService tempDataService,
 IAutomatonValidationService validationService,
 IAutomatonEditingService editingService,
 IAutomatonMinimizationService minimizationService) : Controller
{
    private readonly ILogger<AutomatonCreationController> logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IAutomatonTempDataService tempDataService = tempDataService ?? throw new ArgumentNullException(nameof(tempDataService));
    private readonly IAutomatonValidationService validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
    private readonly IAutomatonEditingService editingService = editingService ?? throw new ArgumentNullException(nameof(editingService));
    private readonly IAutomatonMinimizationService minimizationService = minimizationService ?? throw new ArgumentNullException(nameof(minimizationService));

    // GET create page
    public IActionResult CreateAutomaton()
    {
        return View(new AutomatonViewModel());
    }

    // POST create
    [HttpPost]
    public IActionResult CreateAutomaton(AutomatonViewModel model)
    {
        logger.LogInformation("CreateAutomaton POST Type={Type} States={States} Transitions={Transitions}",
            model.Type, model.States?.Count ?? 0, model.Transitions?.Count ?? 0);

        var (isValid, errors) = validationService.ValidateAutomaton(model);
        if (!isValid)
        {
            foreach (var e in errors) ModelState.AddModelError(string.Empty, e);
            // Store analysis and temp data so Index view can reflect edge cases (tests rely on this)
            try { StoreMinimizationAnalysis(model); } catch { }
            try { tempDataService.StoreCustomAutomaton(TempData, model); } catch { }
            return View(model);
        }

        model.IsCustomAutomaton = true;
        tempDataService.StoreCustomAutomaton(TempData, model);
        StoreMinimizationAnalysis(model);
        return RedirectToAction("Index", "Home");
    }


    [HttpPost]
    public IActionResult AddState(AutomatonViewModel model, int stateId, bool isStart, bool isAccepting)
    {
        var svc = editingService ?? new AutomatonEditingService(new AutomatonValidationService(NullLogger<AutomatonValidationService>.Instance), NullLogger<AutomatonEditingService>.Instance);
        model.States ??= [];
        model.Transitions ??= [];
        if (!ModelState.IsValid)
        {
            return View("CreateAutomaton", model);
        }
        var result = svc.AddState(model, stateId, isStart, isAccepting);
        if (!result.Ok && result.Error != null) ModelState.AddModelError(string.Empty, result.Error);
        StoreMinimizationAnalysis(model);
        return View("CreateAutomaton", model);
    }

    [HttpPost]
    public IActionResult RemoveState(AutomatonViewModel model, int stateId)
    {
        var svc = editingService ?? new AutomatonEditingService(new AutomatonValidationService(NullLogger<AutomatonValidationService>.Instance), NullLogger<AutomatonEditingService>.Instance);
        model.States ??= [];
        model.Transitions ??= [];
        if (!ModelState.IsValid)
        {
            return View("CreateAutomaton", model);
        }
        var result = svc.RemoveState(model, stateId);
        if (!result.Ok && result.Error != null) ModelState.AddModelError(string.Empty, result.Error);
        StoreMinimizationAnalysis(model);
        return View("CreateAutomaton", model);
    }

    [HttpPost]
    public IActionResult AddTransition(AutomatonViewModel model, int fromStateId, int toStateId, string symbol, string? newTransitionStackPop = null, string? newTransitionStackPush = null)
    {
        var svc = editingService ?? new AutomatonEditingService(new AutomatonValidationService(NullLogger<AutomatonValidationService>.Instance), NullLogger<AutomatonEditingService>.Instance);
        model.States ??= [];
        model.Transitions ??= [];
        if (!ModelState.IsValid)
        {
            return View("CreateAutomaton", model);
        }
        var result = svc.AddTransition(model, fromStateId, toStateId, symbol, newTransitionStackPop, newTransitionStackPush);
        if (!result.Ok && result.Error != null) ModelState.AddModelError(string.Empty, result.Error);
        StoreMinimizationAnalysis(model);
        return View("CreateAutomaton", model);
    }

    [HttpPost]
    public IActionResult RemoveTransition(AutomatonViewModel model, int fromStateId, int toStateId, string symbol)
    {
        var svc = editingService ?? new AutomatonEditingService(new AutomatonValidationService(NullLogger<AutomatonValidationService>.Instance), NullLogger<AutomatonEditingService>.Instance);
        model.States ??= [];
        model.Transitions ??= [];
        if (!ModelState.IsValid)
        {
            return View("CreateAutomaton", model);
        }
        var result = svc.RemoveTransition(model, fromStateId, toStateId, symbol);
        if (!result.Ok && result.Error != null) ModelState.AddModelError(string.Empty, result.Error);
        StoreMinimizationAnalysis(model);
        return View("CreateAutomaton", model);
    }

    // Helper to stash analysis in TempData for Index view
    private void StoreMinimizationAnalysis(AutomatonViewModel model)
    {
        try
        {
            var analysis = minimizationService.AnalyzeAutomaton(model);
            TempData["MinimizationAnalysis"] = JsonSerializer.Serialize(analysis);
        }
        catch { /* ignore */ }
    }
}
