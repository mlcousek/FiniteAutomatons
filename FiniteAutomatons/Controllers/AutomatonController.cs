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
    public IActionResult AddTransition(AutomatonViewModel model, int fromStateId, int toStateId, string symbol)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }
        var (ok, _, err) = editingService.AddTransition(model, fromStateId, toStateId, symbol);
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

    [HttpPost]
    public IActionResult StepForward([FromForm] AutomatonViewModel model)
    {
        model.HasExecuted = true;
        var updated = executionService.ExecuteStepForward(model);
        updated.HasExecuted = true;
        return View("../Home/Index", updated);
    }

    [HttpPost]
    public IActionResult StepBackward([FromForm] AutomatonViewModel model)
    {
        var updated = executionService.ExecuteStepBackward(model);
        updated.HasExecuted = model.HasExecuted || updated.Position > 0;
        return View("../Home/Index", updated);
    }

    [HttpPost]
    public IActionResult ExecuteAll([FromForm] AutomatonViewModel model)
    {
        model.HasExecuted = true;
        var updated = executionService.ExecuteAll(model);
        updated.HasExecuted = true;
        return View("../Home/Index", updated);
    }

    [HttpPost]
    public IActionResult BackToStart([FromForm] AutomatonViewModel model)
    {
        var updated = executionService.BackToStart(model);
        updated.HasExecuted = model.HasExecuted || model.Position > 0 || model.Result != null;
        return View("../Home/Index", updated);
    }

    [HttpPost]
    public IActionResult Reset([FromForm] AutomatonViewModel model)
    {
        var updated = executionService.ResetExecution(model);
        updated.HasExecuted = false;
        return View("../Home/Index", updated);
    }

    [HttpPost]
    public IActionResult ConvertToDFA([FromForm] AutomatonViewModel model)
    {
        var converted = conversionService.ConvertToDFA(model);
        converted.ClearExecutionState();
        tempDataService.StoreCustomAutomaton(TempData, converted);
        tempDataService.StoreConversionMessage(TempData, $"Successfully converted {model.TypeDisplayName} to DFA with {converted.States.Count} states.");
        return RedirectToAction("Index", "Home");
    }

    // GET generator page
    public IActionResult GenerateRandomAutomaton() => View(new RandomAutomatonGenerationViewModel
    {
        Type = AutomatonType.DFA,
        StateCount = 5,
        TransitionCount = 8,
        AlphabetSize = 3,
        AcceptingStateRatio = 0.3
    });

    [HttpPost]
    public IActionResult GenerateRandomAutomaton(RandomAutomatonGenerationViewModel model)
    {
        logger.LogInformation("Generating random automaton Type={Type} States={States} Transitions={Transitions}",
            model.Type, model.StateCount, model.TransitionCount);
        if (!generatorService.ValidateGenerationParameters(model.Type, model.StateCount, model.TransitionCount, model.AlphabetSize))
        {
            ModelState.AddModelError(string.Empty, "Invalid generation parameters. Please check your input values.");
            return View(model);
        }
        var generated = generatorService.GenerateRandomAutomaton(model.Type, model.StateCount, model.TransitionCount, model.AlphabetSize, model.AcceptingStateRatio, model.Seed);
        tempDataService.StoreCustomAutomaton(TempData, generated);
        tempDataService.StoreConversionMessage(TempData, $"Successfully generated random {model.Type} with {generated.States.Count} states and {generated.Transitions.Count} transitions.");
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public IActionResult GenerateRealisticAutomaton(AutomatonType type, int stateCount, int? seed = null)
    {
        logger.LogInformation("Generating realistic automaton Type={Type} States={States}", type, stateCount);
        if (stateCount < 1 || stateCount > 20)
        {
            tempDataService.StoreErrorMessage(TempData, "State count must be between 1 and 20.");
            return RedirectToAction("GenerateRandomAutomaton");
        }
        var generated = generatorService.GenerateRealisticAutomaton(type, stateCount, seed);
        tempDataService.StoreCustomAutomaton(TempData, generated);
        tempDataService.StoreConversionMessage(TempData, $"Successfully generated realistic {type} with {generated.States.Count} states and {generated.Transitions.Count} transitions.");
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public async Task<IActionResult> ImportAutomaton(IFormFile upload)
    {
        if (upload == null)
        {
            ModelState.AddModelError(string.Empty, "No file uploaded.");
            return View("CreateAutomaton", new AutomatonViewModel());
        }
        var (ok, model, error) = await fileService.LoadFromFileAsync(upload);
        if (!ok || model == null)
        {
            ModelState.AddModelError(string.Empty, error ?? "Failed to load automaton.");
            return View("CreateAutomaton", new AutomatonViewModel());
        }
        tempDataService.StoreCustomAutomaton(TempData, model);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public IActionResult ExportJson([FromForm] AutomatonViewModel model)
    {
        var (name, content) = fileService.ExportJson(model);
        return File(System.Text.Encoding.UTF8.GetBytes(content), "application/json", name);
    }

    [HttpPost]
    public IActionResult ExportText([FromForm] AutomatonViewModel model)
    {
        var (name, content) = fileService.ExportText(model);
        return File(System.Text.Encoding.UTF8.GetBytes(content), "text/plain", name);
    }
}
