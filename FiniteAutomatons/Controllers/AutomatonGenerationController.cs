using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FiniteAutomatons.Controllers;

public class AutomatonGenerationController(
    IAutomatonGeneratorService generatorService,
    IAutomatonPresetService presetService,
    IAutomatonTempDataService tempDataService,
    ILogger<AutomatonGenerationController> logger) : Controller
{
    private readonly IAutomatonGeneratorService generatorService = generatorService;
    private readonly IAutomatonPresetService presetService = presetService;
    private readonly IAutomatonTempDataService tempDataService = tempDataService;
    private readonly ILogger<AutomatonGenerationController> logger = logger;

    // GET generator page - redirects to Home with a random automaton with varied parameters
    public IActionResult GenerateRandomAutomaton()
    {
        logger.LogInformation("Generating random automaton via GET redirect");

        var (stateCount, transitionCount, alphabetSize, acceptingRatio) = GenerateRandomParameters();

        var generated = generatorService.GenerateRandomAutomaton(
            AutomatonType.DFA,
            stateCount,
            transitionCount,
            alphabetSize,
            acceptingRatio,
            seed: null);

        tempDataService.StoreCustomAutomaton(TempData, generated);
        tempDataService.StoreConversionMessage(TempData,
            $"Generated random DFA with {generated.States.Count} states, {generated.Transitions.Count} transitions, and alphabet size {generated.Alphabet.Count}.");

        return RedirectToAction("Index", "Home");
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

        var generated = generatorService.GenerateRandomAutomaton(
            model.Type,
            model.StateCount,
            model.TransitionCount,
            model.AlphabetSize,
            model.AcceptingStateRatio,
            model.Seed);

        tempDataService.StoreCustomAutomaton(TempData, generated);
        tempDataService.StoreConversionMessage(TempData,
            $"Successfully generated random {model.Type} with {generated.States.Count} states and {generated.Transitions.Count} transitions.");

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public IActionResult GeneratePreset([FromForm] string preset)
    {
        if (string.IsNullOrWhiteSpace(preset))
        {
            tempDataService.StoreErrorMessage(TempData, "No preset specified");
            return RedirectToAction("Index", "Home");
        }

        logger.LogInformation("Generating preset automaton: {Preset}", preset);

        try
        {
            var (stateCount, transitionCount, alphabetSize, acceptingRatio) = GenerateRandomParameters();
            int? seed = null;

            AutomatonViewModel generated = preset.Trim().ToLowerInvariant() switch
            {
                "random-dfa" => presetService.GenerateRandomDfa(stateCount, transitionCount, alphabetSize, acceptingRatio, seed),
                "minimalized-dfa" => presetService.GenerateMinimalizedDfa(stateCount, transitionCount, alphabetSize, acceptingRatio, seed),
                "unminimalized-dfa" => presetService.GenerateUnminimalizedDfa(stateCount, transitionCount, alphabetSize, acceptingRatio, seed),
                "nondet-nfa" => presetService.GenerateNondeterministicNfa(stateCount, transitionCount, alphabetSize, acceptingRatio, seed),
                "random-nfa" => presetService.GenerateRandomNfa(stateCount, transitionCount, alphabetSize, acceptingRatio, seed),
                "enfa-eps" => presetService.GenerateEpsilonNfa(stateCount, transitionCount, alphabetSize, acceptingRatio, seed),
                "enfa-nondet" => presetService.GenerateEpsilonNfaNondeterministic(stateCount, transitionCount, alphabetSize, acceptingRatio, seed),
                "random-enfa" => presetService.GenerateEpsilonNfa(stateCount, transitionCount, alphabetSize, acceptingRatio, seed),
                _ => throw new ArgumentException($"Unknown preset: {preset}")
            };

            tempDataService.StoreCustomAutomaton(TempData, generated);
            tempDataService.StoreConversionMessage(TempData,
                $"Successfully generated {GetPresetDisplayName(preset)} with {generated.States.Count} states and {generated.Transitions.Count} transitions.");

            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate preset automaton: {Preset}", preset);
            tempDataService.StoreErrorMessage(TempData, $"Failed to generate preset automaton: {ex.Message}");
            return RedirectToAction("Index", "Home");
        }
    }

    private static (int stateCount, int transitionCount, int alphabetSize, double acceptingRatio) GenerateRandomParameters()
    {
        var random = new Random();
        var stateCount = random.Next(5, 16);           // 5-15 states
        var transitionCount = random.Next(4, 26);      // 4-25 transitions
        var alphabetSize = random.Next(2, 9);          // 2-8 alphabet size
        var acceptingRatio = 0.2 + random.NextDouble() * 0.3; // 0.2-0.5

        return (stateCount, transitionCount, alphabetSize, acceptingRatio);
    }

    private static string GetPresetDisplayName(string preset) => preset.Trim().ToLowerInvariant() switch
    {
        "minimalized-dfa" => "Minimalized DFA",
        "unminimalized-dfa" => "DFA (un-minimalized)",
        "nondet-nfa" => "Nondeterministic NFA",
        "random-nfa" => "Random NFA",
        "enfa-eps" => "ε-NFA (with ε transitions)",
        "enfa-nondet" => "ε-NFA (nondeterministic)",
        "random-enfa" => "Random ε-NFA",
        _ => preset
    };
}
