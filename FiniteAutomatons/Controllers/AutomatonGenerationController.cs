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
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generating random automaton via GET redirect");
        }

        var (stateCount, transitionCount, alphabetSize, acceptingRatio) = generatorService.GenerateRandomParameters();

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
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generating random automaton Type={Type} States={States} Transitions={Transitions}",
            model.Type, model.StateCount, model.TransitionCount);
        }

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
    public IActionResult GeneratePreset([FromForm] string preset, [FromForm] string? family)
    {
        if (string.IsNullOrWhiteSpace(preset))
        {
            tempDataService.StoreErrorMessage(TempData, "No preset specified");
            return RedirectToAction("Index", "Home");
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generating preset automaton: {Preset}", preset);
        }

        try
        {
            var (stateCount, transitionCount, alphabetSize, acceptingRatio) = generatorService.GenerateRandomParameters();
            int? seed = null;

            var pdaType = ResolvePdaType(family);

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
                "random-pda" => presetService.GenerateRandomPda(stateCount, transitionCount, alphabetSize, acceptingRatio, seed, pdaType: pdaType),
                "pda-pushpop" => presetService.GeneratePdaWithPushPopPairs(stateCount, transitionCount, alphabetSize, acceptingRatio, seed, pdaType: pdaType),
                "pda-balanced-parens" => presetService.GenerateBalancedParenthesesPda(),
                "pda-anbn" => presetService.GenerateAnBnPda(),
                "pda-palindrome" => presetService.GenerateEvenPalindromePda(),
                "pda-cfg-demo" => presetService.GenerateSimpleCfgPda(),
                _ => throw new ArgumentException($"Unknown preset: {preset}")
            };

            tempDataService.StoreCustomAutomaton(TempData, generated);
            tempDataService.StoreConversionMessage(TempData,
                $"Successfully generated {presetService.GetPresetDisplayName(preset)} with {generated.States.Count} states and {generated.Transitions.Count} transitions.");

            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate preset automaton: {Preset}", preset);
            tempDataService.StoreErrorMessage(TempData, $"Failed to generate preset automaton: {ex.Message}");
            return RedirectToAction("Index", "Home");
        }
    }

    private static AutomatonType ResolvePdaType(string? family)
    {
        if (string.Equals(family, "NPDA", StringComparison.OrdinalIgnoreCase))
        {
            return AutomatonType.NPDA;
        }

        return AutomatonType.DPDA;
    }
}
