using FiniteAutomatons.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FiniteAutomatons.Controllers;

public class AutomatonPresetController(IAutomatonGeneratorService generatorService, IAutomatonMinimizationService minimizationService, IAutomatonTempDataService tempDataService) : Controller
{
    private readonly IAutomatonGeneratorService generatorService = generatorService;
    private readonly IAutomatonMinimizationService minimizationService = minimizationService;
    private readonly IAutomatonTempDataService tempDataService = tempDataService;

    [HttpPost]
    public IActionResult GeneratePreset([FromForm] string preset)
    {
        if (string.IsNullOrWhiteSpace(preset))
        {
            tempDataService.StoreErrorMessage(TempData, "No preset specified");
            return RedirectToAction("Index", "Home");
        }

        try
        {
            switch (preset.Trim().ToLowerInvariant())
            {
                case "minimalized-dfa":
                    {
                        var model = generatorService.GenerateRealisticAutomaton(FiniteAutomatons.Core.Models.ViewModel.AutomatonType.DFA, 5);
                        var (minModel, msg) = minimizationService.MinimizeDfa(model);
                        if (minModel != null)
                        {
                            tempDataService.StoreCustomAutomaton(TempData, minModel);
                            tempDataService.StoreConversionMessage(TempData, "Generated preset: Minimalized DFA");
                        }
                        else
                        {
                            tempDataService.StoreErrorMessage(TempData, "Failed to minimize generated DFA.");
                        }
                        break;
                    }
                case "unminimalized-dfa":
                    {
                        var model = generatorService.GenerateRealisticAutomaton(FiniteAutomatons.Core.Models.ViewModel.AutomatonType.DFA, 5);
                        tempDataService.StoreCustomAutomaton(TempData, model);
                        tempDataService.StoreConversionMessage(TempData, "Generated preset: DFA (un-minimalized)");
                        break;
                    }
                case "nfa":
                    {
                        var model = generatorService.GenerateRealisticAutomaton(FiniteAutomatons.Core.Models.ViewModel.AutomatonType.NFA, 5);
                        tempDataService.StoreCustomAutomaton(TempData, model);
                        tempDataService.StoreConversionMessage(TempData, "Generated preset: NFA");
                        break;
                    }
                case "epsilon-nfa":
                    {
                        var model = generatorService.GenerateRealisticAutomaton(FiniteAutomatons.Core.Models.ViewModel.AutomatonType.EpsilonNFA, 5);
                        tempDataService.StoreCustomAutomaton(TempData, model);
                        tempDataService.StoreConversionMessage(TempData, "Generated preset: ?-NFA");
                        break;
                    }
                default:
                    {
                        var model = generatorService.GenerateRealisticAutomaton(FiniteAutomatons.Core.Models.ViewModel.AutomatonType.DFA, 5);
                        tempDataService.StoreCustomAutomaton(TempData, model);
                        tempDataService.StoreConversionMessage(TempData, "Generated preset: DFA");
                        break;
                    }
            }

            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            tempDataService.StoreErrorMessage(TempData, "Failed to generate preset automaton: " + ex.Message);
            return RedirectToAction("Index", "Home");
        }
    }
}
