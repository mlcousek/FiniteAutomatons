using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using FiniteAutomatons.Core.Utilities;

namespace FiniteAutomatons.Controllers;

public class AutomatonController(
    ILogger<AutomatonController> logger,
    IAutomatonGeneratorService generatorService,
    IAutomatonTempDataService tempDataService,
    IAutomatonValidationService validationService,
    IAutomatonConversionService conversionService,
    IAutomatonExecutionService executionService) : Controller
{
    private readonly ILogger<AutomatonController> logger = logger;
    private readonly IAutomatonGeneratorService generatorService = generatorService;
    private readonly IAutomatonTempDataService tempDataService = tempDataService;
    private readonly IAutomatonValidationService validationService = validationService;
    private readonly IAutomatonConversionService conversionService = conversionService;
    private readonly IAutomatonExecutionService executionService = executionService;

    private static bool IsEpsilon(string? s) => AutomatonSymbolHelper.IsEpsilon(s);

    public IActionResult CreateAutomaton()
    {
        var model = new AutomatonViewModel();
        return View(model);
    }

    [HttpPost]
    [IgnoreAntiforgeryToken] // Allow integration tests to work without antiforgery tokens
    public IActionResult CreateAutomaton(AutomatonViewModel model)
    {
        try
        {
            logger.LogInformation("CreateAutomaton POST called with Type: {Type}, States: {StateCount}, Transitions: {TransitionCount}",
                model.Type, model.States?.Count ?? 0, model.Transitions?.Count ?? 0);

            // Ensure collections are initialized
            model.States ??= [];
            model.Transitions ??= [];
            model.Alphabet ??= [];

            // Log transition symbols for debugging
            foreach (var transition in model.Transitions)
            {
                logger.LogInformation("Transition: {From} -> {To} on '{Symbol}' (char code: {Code})",
                    transition.FromStateId, transition.ToStateId,
                    transition.Symbol == AutomatonSymbolHelper.EpsilonInternal ? AutomatonSymbolHelper.EpsilonDisplay : transition.Symbol.ToString(),
                    (int)transition.Symbol);
            }

            // Validate the automaton using the service
            var (isValid, errors) = validationService.ValidateAutomaton(model);
            if (!isValid)
            {
                logger.LogWarning("Automaton validation failed: {Errors}", string.Join("; ", errors));
                foreach (var error in errors) ModelState.AddModelError("", error);
                return View(model);
            }

            // Mark as custom automaton
            model.IsCustomAutomaton = true;

            // Store the custom automaton using the service
            tempDataService.StoreCustomAutomaton(TempData, model);

            logger.LogInformation("Successfully created automaton, redirecting to Index");
            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating automaton");
            ModelState.AddModelError("", "An error occurred while creating the automaton.");
            return View(model);
        }
    }

    [HttpPost]
    [IgnoreAntiforgeryToken] // Allow integration tests to work without antiforgery tokens
    public IActionResult ChangeAutomatonType(AutomatonViewModel model, AutomatonType newType)
    {
        try
        {
            // Ensure collections are initialized
            model.States ??= [];
            model.Transitions ??= [];
            model.Alphabet ??= [];

            if (model.Type == newType)
                return View("CreateAutomaton", model);

            // Convert the automaton to the new type if possible using the service
            var (convertedModel, warnings) = conversionService.ConvertAutomatonType(model, newType);
            foreach (var warning in warnings) ModelState.AddModelError("", warning);

            // Clear any stale execution state after type change
            ClearExecutionState(convertedModel);
            return View("CreateAutomaton", convertedModel);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error changing automaton type");
            ModelState.AddModelError("", "An error occurred while changing the automaton type.");
            return View("CreateAutomaton", model);
        }
    }

    [HttpPost]
    [IgnoreAntiforgeryToken] // Allow integration tests to work without antiforgery tokens
    public IActionResult AddState(AutomatonViewModel model, int stateId, bool isStart, bool isAccepting)
    {
        try
        {
            // Ensure collections are initialized
            model.States ??= [];
            model.Transitions ??= [];
            model.Alphabet ??= [];

            // Validate state addition using the service
            var (isValidState, errorMessage) = validationService.ValidateStateAddition(model, stateId, isStart);
            if (!isValidState)
            {
                ModelState.AddModelError("", errorMessage!);
                return View("CreateAutomaton", model);
            }

            model.States.Add(new State { Id = stateId, IsStart = isStart, IsAccepting = isAccepting });
            // Adding structure changes invalidates execution state
            ClearExecutionState(model, keepInput: true);
            return View("CreateAutomaton", model);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding state");
            ModelState.AddModelError("", "An error occurred while adding the state.");
            return View("CreateAutomaton", model);
        }
    }

    [HttpPost]
    [IgnoreAntiforgeryToken] // Allow integration tests to work without antiforgery tokens
    public IActionResult AddTransition(AutomatonViewModel model, int fromStateId, int toStateId, string symbol)
    {
        try
        {
            // Ensure collections are initialized
            model.States ??= [];
            model.Transitions ??= [];
            model.Alphabet ??= [];

            // Validate transition addition using the service
            var (isValidTransition, processedSymbol, transitionError) = validationService.ValidateTransitionAddition(model, fromStateId, toStateId, symbol ?? string.Empty);
            if (!isValidTransition)
            {
                ModelState.AddModelError("", transitionError!);
                return View("CreateAutomaton", model);
            }

            model.Transitions.Add(new Transition { FromStateId = fromStateId, ToStateId = toStateId, Symbol = processedSymbol });
            // Update alphabet (but not for epsilon transitions)
            if (processedSymbol != AutomatonSymbolHelper.EpsilonInternal && !model.Alphabet.Contains(processedSymbol))
                model.Alphabet.Add(processedSymbol);

            logger.LogInformation("Transition added successfully: {From} -> {To} on '{Symbol}' (char code: {Code})",
                fromStateId, toStateId, processedSymbol == AutomatonSymbolHelper.EpsilonInternal ? AutomatonSymbolHelper.EpsilonDisplay : processedSymbol.ToString(), (int)processedSymbol);

            ClearExecutionState(model, keepInput: true);
            return View("CreateAutomaton", model);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding transition");
            ModelState.AddModelError("", "An error occurred while adding the transition.");
            return View("CreateAutomaton", model);
        }
    }

    [HttpPost]
    [IgnoreAntiforgeryToken] // Allow integration tests to work without antiforgery tokens
    public IActionResult RemoveState(AutomatonViewModel model, int stateId)
    {
        try
        {
            // Ensure collections are initialized
            model.States ??= [];
            model.Transitions ??= [];
            model.Alphabet ??= [];

            var removedStart = model.States.FirstOrDefault(s => s.Id == stateId)?.IsStart == true;
            model.States.RemoveAll(s => s.Id == stateId);
            model.Transitions.RemoveAll(t => t.FromStateId == stateId || t.ToStateId == stateId);

            // Update alphabet - remove symbols that are no longer used
            var usedSymbols = model.Transitions.Where(t => t.Symbol != AutomatonSymbolHelper.EpsilonInternal).Select(t => t.Symbol).Distinct().ToList();
            model.Alphabet.RemoveAll(c => !usedSymbols.Contains(c));

            if (removedStart && model.States.Any())
            {
                // Auto-assign first state as start to keep model valid; user can adjust
                model.States[0].IsStart = true;
                ModelState.AddModelError("", "Start state removed. First remaining state marked as new start.");
            }

            ClearExecutionState(model);
            return View("CreateAutomaton", model);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error removing state");
            ModelState.AddModelError("", "An error occurred while removing the state.");
            return View("CreateAutomaton", model);
        }
    }

    [HttpPost]
    [IgnoreAntiforgeryToken] // Allow integration tests to work without antiforgery tokens
    public IActionResult RemoveTransition(AutomatonViewModel model, int fromStateId, int toStateId, string symbol)
    {
        try
        {
            // Ensure collections are initialized
            model.States ??= [];
            model.Transitions ??= [];
            model.Alphabet ??= [];

            char symbolChar;
            if (IsEpsilon(symbol))
            {
                symbolChar = AutomatonSymbolHelper.EpsilonInternal;
            }
            else if (!string.IsNullOrWhiteSpace(symbol) && symbol.Trim().Length == 1)
            {
                symbolChar = symbol.Trim()[0];
            }
            else
            {
                ModelState.AddModelError("", "Invalid symbol format.");
                return View("CreateAutomaton", model);
            }

            int removed = model.Transitions.RemoveAll(t => t.FromStateId == fromStateId && t.ToStateId == toStateId && t.Symbol == symbolChar);
            if (removed == 0)
            {
                ModelState.AddModelError("", "No matching transition found to remove.");
            }

            // Update alphabet - remove symbol if no longer used
            if (symbolChar != AutomatonSymbolHelper.EpsilonInternal && !model.Transitions.Any(t => t.Symbol == symbolChar))
                model.Alphabet.Remove(symbolChar);

            ClearExecutionState(model);
            return View("CreateAutomaton", model);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error removing transition");
            ModelState.AddModelError("", "An error occurred while removing the transition.");
            return View("CreateAutomaton", model);
        }
    }

    [HttpPost]
    public IActionResult StepForward([FromForm] AutomatonViewModel model)
    {
        try
        {
            NormalizeEpsilonTransitions(model);
            model.HasExecuted = true;
            var updatedModel = executionService.ExecuteStepForward(model);
            updatedModel.HasExecuted = true;
            return View("../Home/Index", updatedModel);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in StepForward");
            tempDataService.StoreErrorMessage(TempData, "An error occurred while stepping forward.");
            return View("../Home/Index", model);
        }
    }

    [HttpPost]
    public IActionResult StepBackward([FromForm] AutomatonViewModel model)
    {
        try
        {
            NormalizeEpsilonTransitions(model);
            var updatedModel = executionService.ExecuteStepBackward(model);
            updatedModel.HasExecuted = model.HasExecuted || updatedModel.Position > 0;
            return View("../Home/Index", updatedModel);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in StepBackward");
            tempDataService.StoreErrorMessage(TempData, "An error occurred while stepping backward.");
            return View("../Home/Index", model);
        }
    }

    [HttpPost]
    public IActionResult ExecuteAll([FromForm] AutomatonViewModel model)
    {
        try
        {
            NormalizeEpsilonTransitions(model);
            model.HasExecuted = true;
            var updatedModel = executionService.ExecuteAll(model);
            updatedModel.HasExecuted = true;
            return View("../Home/Index", updatedModel);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in ExecuteAll");
            tempDataService.StoreErrorMessage(TempData, "An error occurred while executing the automaton.");
            return View("../Home/Index", model);
        }
    }

    [HttpPost]
    public IActionResult BackToStart([FromForm] AutomatonViewModel model)
    {
        try
        {
            NormalizeEpsilonTransitions(model);
            var updatedModel = executionService.BackToStart(model);
            updatedModel.HasExecuted = model.HasExecuted || model.Position > 0 || model.Result != null;
            return View("../Home/Index", updatedModel);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in BackToStart");
            tempDataService.StoreErrorMessage(TempData, "An error occurred while resetting to start.");
            return View("../Home/Index", model);
        }
    }

    [HttpPost]
    public IActionResult Reset([FromForm] AutomatonViewModel model)
    {
        try
        {
            NormalizeEpsilonTransitions(model);
            var updatedModel = executionService.ResetExecution(model);
            updatedModel.HasExecuted = false;
            return View("../Home/Index", updatedModel);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in Reset");
            tempDataService.StoreErrorMessage(TempData, "An error occurred while resetting.");
            return View("../Home/Index", model);
        }
    }

    [HttpPost]
    public IActionResult ConvertToDFA([FromForm] AutomatonViewModel model)
    {
        try
        {
            NormalizeEpsilonTransitions(model);
            var convertedModel = conversionService.ConvertToDFA(model);
            ClearExecutionState(convertedModel);
            tempDataService.StoreCustomAutomaton(TempData, convertedModel);
            tempDataService.StoreConversionMessage(TempData, $"Successfully converted {model.TypeDisplayName} to DFA with {convertedModel.States.Count} states.");
            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in ConvertToDFA");
            tempDataService.StoreErrorMessage(TempData, $"Failed to convert to DFA: {ex.Message}");
            return View("../Home/Index", model);
        }
    }

    public IActionResult GenerateRandomAutomaton()
    {
        var model = new RandomAutomatonGenerationViewModel
        {
            Type = AutomatonType.DFA,
            StateCount = 5,
            TransitionCount = 8,
            AlphabetSize = 3,
            AcceptingStateRatio = 0.3
        };
        return View(model);
    }

    [HttpPost]
    public IActionResult GenerateRandomAutomaton(RandomAutomatonGenerationViewModel model)
    {
        try
        {
            logger.LogInformation("Generating random automaton: Type={Type}, States={States}, Transitions={Transitions}",
                model.Type, model.StateCount, model.TransitionCount);

            // Validate parameters
            if (!generatorService.ValidateGenerationParameters(model.Type, model.StateCount, model.TransitionCount, model.AlphabetSize))
            {
                ModelState.AddModelError("", "Invalid generation parameters. Please check your input values.");
                return View(model);
            }

            // Generate the automaton
            var generatedAutomaton = generatorService.GenerateRandomAutomaton(
                model.Type,
                model.StateCount,
                model.TransitionCount,
                model.AlphabetSize,
                model.AcceptingStateRatio,
                model.Seed);

            // Store in TempData and redirect to simulator using the service
            tempDataService.StoreCustomAutomaton(TempData, generatedAutomaton);
            tempDataService.StoreConversionMessage(TempData, $"Successfully generated random {model.Type} with {generatedAutomaton.States.Count} states and {generatedAutomaton.Transitions.Count} transitions.");
            logger.LogInformation("Successfully generated random automaton, redirecting to Index");
            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating random automaton");
            ModelState.AddModelError("", $"An error occurred while generating the automaton: {ex.Message}");
            return View(model);
        }
    }

    [HttpPost]
    public IActionResult GenerateRealisticAutomaton(AutomatonType type, int stateCount, int? seed = null)
    {
        try
        {
            logger.LogInformation("Generating realistic automaton: Type={Type}, States={States}", type, stateCount);

            if (stateCount < 1 || stateCount > 20)
            {
                tempDataService.StoreErrorMessage(TempData, "State count must be between 1 and 20.");
                return RedirectToAction("GenerateRandomAutomaton");
            }

            // Generate the automaton with realistic parameters
            var generatedAutomaton = generatorService.GenerateRealisticAutomaton(type, stateCount, seed);

            // Store in TempData and redirect to simulator using the service
            tempDataService.StoreCustomAutomaton(TempData, generatedAutomaton);
            tempDataService.StoreConversionMessage(TempData, $"Successfully generated realistic {type} with {generatedAutomaton.States.Count} states and {generatedAutomaton.Transitions.Count} transitions.");
            logger.LogInformation("Successfully generated realistic automaton, redirecting to Index");
            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating realistic automaton");
            tempDataService.StoreErrorMessage(TempData, $"An error occurred while generating the automaton: {ex.Message}");
            return RedirectToAction("GenerateRandomAutomaton");
        }
    }

    private static void ClearExecutionState(AutomatonViewModel model, bool keepInput = false)
    {
        if (!keepInput) model.Input = string.Empty;
        model.Result = null;
        model.CurrentStateId = null;
        model.CurrentStates = null;
        model.Position = 0;
        model.IsAccepted = null;
        model.StateHistorySerialized = string.Empty;
    }

    private static void NormalizeEpsilonTransitions(AutomatonViewModel model)
    {
        if (model.Transitions == null) return;
        foreach (var t in model.Transitions)
        {
            if (t.Symbol == '?' || t.Symbol == '?' )
            {
                t.Symbol = AutomatonSymbolHelper.EpsilonInternal;
            }
        }
    }
}
