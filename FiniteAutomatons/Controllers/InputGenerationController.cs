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
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generating random string with length {MinLength}-{MaxLength}", minLength, maxLength);
        }

        if (!ValidateModel(model)) return RedirectToAction("Index", "Home");

        ResetExecutionState(model);
        var generatedString = inputGenerationService.GenerateRandomString(model, minLength, maxLength);
        model.Input = generatedString;

        return ProcessGenerationResult(model, $"Generated random string: '{generatedString}' (length: {generatedString.Length})");
    }

    [HttpPost]
    public IActionResult GenerateAcceptingString(AutomatonViewModel model, int maxLength = 20)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generating accepting string with max length {MaxLength}", maxLength);
        }

        if (!ValidateModel(model)) return RedirectToAction("Index", "Home");

        ResetExecutionState(model);

        var generatedString = inputGenerationService.GenerateAcceptingString(model, maxLength);

        if (generatedString == null)
        {
            return ProcessGenerationFailure(model,
                "Could not generate an accepting string. The automaton may have no accepting states or no path to them.");
        }

        model.Input = generatedString;
        return ProcessGenerationResult(model, $"Generated shortest accepting string: '{generatedString}' (length: {generatedString.Length})");
    }

    [HttpPost]
    public IActionResult GenerateRandomAcceptingString(AutomatonViewModel model, int minLength = 0, int maxLength = 50, int maxAttempts = 100)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generating random accepting string with length {MinLength}-{MaxLength}, attempts {MaxAttempts}",
                minLength, maxLength, maxAttempts);
        }

        if (!ValidateModel(model)) return RedirectToAction("Index", "Home");

        ResetExecutionState(model);
        var generatedString = inputGenerationService.GenerateRandomAcceptingString(model, minLength, maxLength, maxAttempts);

        if (generatedString == null)
        {
            return ProcessGenerationFailure(model,
                "Could not generate a random accepting string. The automaton may have no accepting states or no reachable path to them.");
        }

        model.Input = generatedString;
        return ProcessGenerationResult(model, $"Generated random accepting string: '{generatedString}' (length: {generatedString.Length})");
    }

    [HttpPost]
    public IActionResult GenerateRejectingString(AutomatonViewModel model, int maxLength = 20)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generating rejecting string with max length {MaxLength}", maxLength);
        }

        if (!ValidateModel(model)) return RedirectToAction("Index", "Home");

        ResetExecutionState(model);
        var generatedString = inputGenerationService.GenerateRejectingString(model, maxLength);

        if (generatedString == null)
        {
            return ProcessGenerationFailure(model,
                "Could not generate a rejecting string. The automaton may accept all strings.");
        }

        model.Input = generatedString;
        return ProcessGenerationResult(model, $"Generated rejecting string: '{generatedString}' (length: {generatedString.Length})");
    }

    [HttpPost]
    public IActionResult GenerateInterestingCase(AutomatonViewModel model, string caseType = "accepting")
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generating interesting case of type: {CaseType}", caseType);
        }

        if (!ValidateModel(model)) return RedirectToAction("Index", "Home");

        ResetExecutionState(model);
        var cases = inputGenerationService.GenerateInterestingCases(model);

        if (cases.Count == 0)
        {
            return ProcessGenerationFailure(model, "Could not generate interesting test cases.");
        }

        var selectedCase = cases.FirstOrDefault(c => c.Description.Contains(caseType, StringComparison.InvariantCultureIgnoreCase));
        if (selectedCase == default)
        {
            selectedCase = cases[0];
        }

        model.Input = selectedCase.Input;
        return ProcessGenerationResult(model, $"Generated test case: {selectedCase.Description} → '{selectedCase.Input}'");
    }

    [HttpPost]
    public IActionResult GenerateNondeterministicCase(AutomatonViewModel model, int maxLength = 15)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generating nondeterministic test case");
        }

        if (!ValidateModel(model)) return RedirectToAction("Index", "Home");

        ResetExecutionState(model);
        var generatedString = inputGenerationService.GenerateNondeterministicCase(model, maxLength);

        if (generatedString == null)
        {
            return ProcessGenerationFailure(model,
                "Could not generate a nondeterministic case. The automaton may be deterministic.");
        }

        model.Input = generatedString;
        return ProcessGenerationResult(model, $"Generated nondeterministic test case: '{generatedString}'");
    }

    [HttpPost]
    public IActionResult GenerateEpsilonCase(AutomatonViewModel model, int maxLength = 15)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generating epsilon transition test case");
        }

        if (!ValidateModel(model)) return RedirectToAction("Index", "Home");

        ResetExecutionState(model);
        var generatedString = inputGenerationService.GenerateEpsilonCase(model, maxLength);

        if (generatedString == null)
        {
            return ProcessGenerationFailure(model,
                "Could not generate an epsilon case. The automaton may have no ε-transitions.");
        }

        model.Input = generatedString;
        return ProcessGenerationResult(model, $"Generated ε-transition test case: '{generatedString}'");
    }

    private bool ValidateModel(AutomatonViewModel? model)
    {
        if (model == null || model.States == null || model.States.Count == 0)
        {
            tempDataService.StoreErrorMessage(TempData, "No automaton loaded. Please load or create an automaton first.");
            return false;
        }
        return true;
    }

    private static void ResetExecutionState(AutomatonViewModel model)
    {
        model.HasExecuted = false;
        model.Position = 0;
        model.CurrentStateId = null;
        model.CurrentStates = null;
        model.IsAccepted = null;
        model.StateHistorySerialized = string.Empty;
    }

    private IActionResult ProcessGenerationResult(AutomatonViewModel model, string message)
    {
        tempDataService.StoreCustomAutomaton(TempData, model);
        tempDataService.StoreConversionMessage(TempData, message);
        return RedirectToAction("Index", "Home");
    }

    private IActionResult ProcessGenerationFailure(AutomatonViewModel model, string errorMessage)
    {
        tempDataService.StoreCustomAutomaton(TempData, model);
        tempDataService.StoreErrorMessage(TempData, errorMessage);
        return RedirectToAction("Index", "Home");
    }
}
