using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FiniteAutomatons.Controllers;

public class InputGenerationController(
    IInputGenerationService inputGenerationService,
    IAutomatonTempDataService tempDataService,
    ILogger<InputGenerationController> logger) : Controller
{
    private readonly IInputGenerationService inputGenerationService = inputGenerationService;
    private readonly IAutomatonTempDataService tempDataService = tempDataService;
    private readonly ILogger<InputGenerationController> logger = logger;

    [HttpPost]
    public IActionResult GenerateRandomString(AutomatonViewModel model, int minLength = 0, int maxLength = 10)
    {
        logger.LogInformation("Generating random string with length {MinLength}-{MaxLength}", minLength, maxLength);

        if (model == null || model.States == null || model.States.Count == 0)
        {
            tempDataService.StoreErrorMessage(TempData, "No automaton loaded. Please load or create an automaton first.");
            return RedirectToAction("Index", "Home");
        }

        // Reset execution state before generating new input
        model.HasExecuted = false;
        model.Position = 0;
        model.CurrentStateId = null;
        model.CurrentStates = null;
        model.IsAccepted = null;
        model.StateHistorySerialized = string.Empty;

        var generatedString = inputGenerationService.GenerateRandomString(model, minLength, maxLength);

        model.Input = generatedString;
        tempDataService.StoreCustomAutomaton(TempData, model);
        tempDataService.StoreConversionMessage(TempData,
            $"Generated random string: '{generatedString}' (length: {generatedString.Length})");

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public IActionResult GenerateAcceptingString(AutomatonViewModel model, int maxLength = 20)
    {
        logger.LogInformation("Generating accepting string with max length {MaxLength}", maxLength);

        if (model == null || model.States == null || model.States.Count == 0)
        {
            tempDataService.StoreErrorMessage(TempData, "No automaton loaded. Please load or create an automaton first.");
            return RedirectToAction("Index", "Home");
        }

        // Reset execution state before generating new input
        model.HasExecuted = false;
        model.Position = 0;
        model.CurrentStateId = null;
        model.CurrentStates = null;
        model.IsAccepted = null;
        model.StateHistorySerialized = string.Empty;

        var generatedString = inputGenerationService.GenerateAcceptingString(model, maxLength);

        if (generatedString == null)
        {
            // Keep the automaton even when generation fails
            tempDataService.StoreCustomAutomaton(TempData, model);
            tempDataService.StoreErrorMessage(TempData,
                "Could not generate an accepting string. The automaton may have no accepting states or no path to them.");
            return RedirectToAction("Index", "Home");
        }

        model.Input = generatedString;
        tempDataService.StoreCustomAutomaton(TempData, model);
        tempDataService.StoreConversionMessage(TempData,
            $"Generated shortest accepting string: '{generatedString}' (length: {generatedString.Length})");

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public IActionResult GenerateRandomAcceptingString(AutomatonViewModel model, int minLength = 0, int maxLength = 50, int maxAttempts = 100)
    {
        logger.LogInformation("Generating random accepting string with length {MinLength}-{MaxLength}, attempts {MaxAttempts}", 
            minLength, maxLength, maxAttempts);

        if (model == null || model.States == null || model.States.Count == 0)
        {
            tempDataService.StoreErrorMessage(TempData, "No automaton loaded. Please load or create an automaton first.");
            return RedirectToAction("Index", "Home");
        }

        // Reset execution state before generating new input
        model.HasExecuted = false;
        model.Position = 0;
        model.CurrentStateId = null;
        model.CurrentStates = null;
        model.IsAccepted = null;
        model.StateHistorySerialized = string.Empty;

        var generatedString = inputGenerationService.GenerateRandomAcceptingString(model, minLength, maxLength, maxAttempts);

        if (generatedString == null)
        {
            // Keep the automaton even when generation fails
            tempDataService.StoreCustomAutomaton(TempData, model);
            tempDataService.StoreErrorMessage(TempData,
                "Could not generate a random accepting string. The automaton may have no accepting states or no reachable path to them.");
            return RedirectToAction("Index", "Home");
        }

        model.Input = generatedString;
        tempDataService.StoreCustomAutomaton(TempData, model);
        tempDataService.StoreConversionMessage(TempData,
            $"Generated random accepting string: '{generatedString}' (length: {generatedString.Length})");

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public IActionResult GenerateRejectingString(AutomatonViewModel model, int maxLength = 20)
    {
        logger.LogInformation("Generating rejecting string with max length {MaxLength}", maxLength);

        if (model == null || model.States == null || model.States.Count == 0)
        {
            tempDataService.StoreErrorMessage(TempData, "No automaton loaded. Please load or create an automaton first.");
            return RedirectToAction("Index", "Home");
        }

        // Reset execution state before generating new input
        model.HasExecuted = false;
        model.Position = 0;
        model.CurrentStateId = null;
        model.CurrentStates = null;
        model.IsAccepted = null;
        model.StateHistorySerialized = string.Empty;

        var generatedString = inputGenerationService.GenerateRejectingString(model, maxLength);

        if (generatedString == null)
        {
            // Keep the automaton even when generation fails
            tempDataService.StoreCustomAutomaton(TempData, model);
            tempDataService.StoreErrorMessage(TempData,
                "Could not generate a rejecting string. The automaton may accept all strings.");
            return RedirectToAction("Index", "Home");
        }

        model.Input = generatedString;
        tempDataService.StoreCustomAutomaton(TempData, model);
        tempDataService.StoreConversionMessage(TempData,
            $"Generated rejecting string: '{generatedString}' (length: {generatedString.Length})");

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public IActionResult GenerateInterestingCase(AutomatonViewModel model, string caseType = "accepting")
    {
        logger.LogInformation("Generating interesting case of type: {CaseType}", caseType);

        if (model == null || model.States == null || model.States.Count == 0)
        {
            tempDataService.StoreErrorMessage(TempData, "No automaton loaded. Please load or create an automaton first.");
            return RedirectToAction("Index", "Home");
        }

        // Reset execution state before generating new input
        model.HasExecuted = false;
        model.Position = 0;
        model.CurrentStateId = null;
        model.CurrentStates = null;
        model.IsAccepted = null;
        model.StateHistorySerialized = string.Empty;

        var cases = inputGenerationService.GenerateInterestingCases(model);

        if (cases.Count == 0)
        {
            // Keep the automaton even when generation fails
            tempDataService.StoreCustomAutomaton(TempData, model);
            tempDataService.StoreErrorMessage(TempData, "Could not generate interesting test cases.");
            return RedirectToAction("Index", "Home");
        }

        // Find the requested case type or default to first case
        var selectedCase = cases.FirstOrDefault(c => c.Description.ToLowerInvariant().Contains(caseType.ToLowerInvariant()));
        if (selectedCase == default)
        {
            selectedCase = cases[0];
        }

        model.Input = selectedCase.Input;
        tempDataService.StoreCustomAutomaton(TempData, model);
        tempDataService.StoreConversionMessage(TempData,
            $"Generated test case: {selectedCase.Description} → '{selectedCase.Input}'");

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public IActionResult GenerateNondeterministicCase(AutomatonViewModel model, int maxLength = 15)
    {
        logger.LogInformation("Generating nondeterministic test case");

        if (model == null || model.States == null || model.States.Count == 0)
        {
            tempDataService.StoreErrorMessage(TempData, "No automaton loaded. Please load or create an automaton first.");
            return RedirectToAction("Index", "Home");
        }

        // Reset execution state before generating new input
        model.HasExecuted = false;
        model.Position = 0;
        model.CurrentStateId = null;
        model.CurrentStates = null;
        model.IsAccepted = null;
        model.StateHistorySerialized = string.Empty;

        var generatedString = inputGenerationService.GenerateNondeterministicCase(model, maxLength);

        if (generatedString == null)
        {
            // Keep the automaton even when generation fails
            tempDataService.StoreCustomAutomaton(TempData, model);
            tempDataService.StoreErrorMessage(TempData,
                "Could not generate a nondeterministic case. The automaton may be deterministic.");
            return RedirectToAction("Index", "Home");
        }

        model.Input = generatedString;
        tempDataService.StoreCustomAutomaton(TempData, model);
        tempDataService.StoreConversionMessage(TempData,
            $"Generated nondeterministic test case: '{generatedString}'");

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public IActionResult GenerateEpsilonCase(AutomatonViewModel model, int maxLength = 15)
    {
        logger.LogInformation("Generating epsilon transition test case");

        if (model == null || model.States == null || model.States.Count == 0)
        {
            tempDataService.StoreErrorMessage(TempData, "No automaton loaded. Please load or create an automaton first.");
            return RedirectToAction("Index", "Home");
        }

        // Reset execution state before generating new input
        model.HasExecuted = false;
        model.Position = 0;
        model.CurrentStateId = null;
        model.CurrentStates = null;
        model.IsAccepted = null;
        model.StateHistorySerialized = string.Empty;

        var generatedString = inputGenerationService.GenerateEpsilonCase(model, maxLength);

        if (generatedString == null)
        {
            // Keep the automaton even when generation fails
            tempDataService.StoreCustomAutomaton(TempData, model);
            tempDataService.StoreErrorMessage(TempData,
                "Could not generate an epsilon case. The automaton may have no ε-transitions.");
            return RedirectToAction("Index", "Home");
        }

        model.Input = generatedString;
        tempDataService.StoreCustomAutomaton(TempData, model);
        tempDataService.StoreConversionMessage(TempData,
            $"Generated ε-transition test case: '{generatedString}'");

        return RedirectToAction("Index", "Home");
    }
}
