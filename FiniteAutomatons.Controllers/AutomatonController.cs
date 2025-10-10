using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Core.Utilities;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace FiniteAutomatons.Controllers;

public class AutomatonController(
    ILogger<AutomatonController> logger,
    IAutomatonGeneratorService generatorService,
    IAutomatonTempDataService tempDataService,
    IAutomatonValidationService validationService,
    IAutomatonConversionService conversionService,
    IAutomatonExecutionService executionService,
    IAutomatonEditingService editingService,
    IAutomatonFileService fileService) : Controller
{
    private readonly ILogger<AutomatonController> logger = logger;
    private readonly IAutomatonGeneratorService generatorService = generatorService;
    private readonly IAutomatonTempDataService tempDataService = tempDataService;
    private readonly IAutomatonValidationService validationService = validationService;
    private readonly IAutomatonConversionService conversionService = conversionService;
    private readonly IAutomatonExecutionService executionService = executionService;
    private readonly IAutomatonEditingService editingService = editingService;
    private readonly IAutomatonFileService fileService = fileService;

    // GET create page
    public IActionResult CreateAutomaton() => View(new AutomatonViewModel());

    // POST create
    [HttpPost]
    public IActionResult CreateAutomaton(AutomatonViewModel model)
    {
        logger.LogInformation("CreateAutomaton POST Type={Type} States={States} Transitions={Transitions}",
            model.Type, model.States.Count, model.Transitions.Count);

        var (isValid, errors) = validationService.ValidateAutomaton(model);
        if (!isValid)
        {
            foreach (var e in errors) ModelState.AddModelError(string.Empty, e);
            return View(model);
        }

        model.IsCustomAutomaton = true;
        tempDataService.StoreCustomAutomaton(TempData, model);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public IActionResult ChangeAutomatonType(AutomatonViewModel model, AutomatonType newType)
    {
        if (model.Type == newType) return View("CreateAutomaton", model);
        var (converted, warnings) = conversionService.ConvertAutomatonType(model, newType);
        foreach (var w in warnings) ModelState.AddModelError(string.Empty, w);
        converted.ClearExecutionState();
        return View("CreateAutomaton", converted);
    }

    [HttpPost]
    public IActionResult AddState(AutomatonViewModel model, int stateId, bool isStart, bool isAccepting)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }
        var (ok, err) = editingService.AddState(model, stateId, isStart, isAccepting);
        if (!ok) ModelState.AddModelError(string.Empty, err!);
        return View("CreateAutomaton", model);
    }

    [HttpPost]
    public IActionResult RemoveState(AutomatonViewModel model, int stateId)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }
        var (ok, err) = editingService.RemoveState(model, stateId);
        if (!ok) ModelState.AddModelError(string.Empty, err!);
        return View("CreateAutomaton", model);
    }

    [HttpPost]
    public IActionResult AddTransition(AutomatonViewModel model, int fromStateId, int toStateId, string symbol, string? newTransitionStackPop = null, string? newTransitionStackPush = null)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }
        var (ok, _, err) = editingService.AddTransition(model, fromStateId, toStateId, symbol, newTransitionStackPop, newTransitionStackPush);
        if (!ok) ModelState.AddModelError(string.Empty, err!);
        return View("CreateAutomaton", model);
    }

    [HttpPost]
    public IActionResult RemoveTransition(AutomatonViewModel model, int fromStateId, int toStateId, string symbol)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }
        var (ok, err) = editingService.RemoveTransition(model, fromStateId, toStateId, symbol);
        if (!ok) ModelState.AddModelError(string.Empty, err!);
        return View("CreateAutomaton", model);
    }

// ... rest unchanged ...
}
