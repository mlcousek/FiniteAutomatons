using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FiniteAutomatons.Controllers;

public class AutomatonGenerationController(IAutomatonGeneratorService generatorService, IAutomatonTempDataService tempDataService, ILogger<AutomatonGenerationController> logger) : Controller
{
    private readonly IAutomatonGeneratorService generatorService = generatorService;
    private readonly IAutomatonTempDataService tempDataService = tempDataService;
    private readonly ILogger<AutomatonGenerationController> logger = logger;

    // GET generator page
    public IActionResult GenerateRandomAutomaton()
    {
        return View(new RandomAutomatonGenerationViewModel
        {
            Type = AutomatonType.DFA,
            StateCount = 5,
            TransitionCount = 8,
            AlphabetSize = 3,
            AcceptingStateRatio = 0.3
        });
    }

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
}
