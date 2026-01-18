using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Core.Utilities;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace FiniteAutomatons.Controllers;

public class AutomatonConversionController(
    ILogger<AutomatonConversionController> logger,
    IAutomatonConversionService conversionService,
    IAutomatonTempDataService tempDataService,
    IAutomatonMinimizationService minimizationService) : Controller
{
    private readonly ILogger<AutomatonConversionController> logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IAutomatonConversionService conversionService = conversionService ?? throw new ArgumentNullException(nameof(conversionService));
    private readonly IAutomatonTempDataService tempDataService = tempDataService ?? throw new ArgumentNullException(nameof(tempDataService));
    private readonly IAutomatonMinimizationService minimizationService = minimizationService ?? throw new ArgumentNullException(nameof(minimizationService));

    [HttpPost]
    public IActionResult ConvertToDFA([FromForm] AutomatonViewModel model)
    {
        var converted = conversionService.ConvertToDFA(model);
        converted.ClearExecutionState();
        tempDataService.StoreCustomAutomaton(TempData, converted);
        tempDataService.StoreConversionMessage(TempData, $"Successfully converted {model.TypeDisplayName} to DFA with {converted.States.Count} states.");
        StoreMinimizationAnalysis(converted);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public IActionResult ChangeAutomatonType(AutomatonViewModel model, AutomatonType newType)
    {
        if (model.Type == newType) return View("CreateAutomaton", model);
        var (converted, warnings) = conversionService.ConvertAutomatonType(model, newType);
        foreach (var w in warnings) ModelState.AddModelError(string.Empty, w);
        converted.ClearExecutionState();
        StoreMinimizationAnalysis(converted);
        return View("CreateAutomaton", converted);
    }

    [HttpPost]
    public IActionResult SwitchType([FromForm] AutomatonViewModel model, AutomatonType targetType)
    {
        if (model == null) return RedirectToAction("Index", "Home");
        if (model.Type == targetType) return RedirectToAction("Index", "Home");

        // Allowed conversion paths triggered by UI
        bool allowed =
            (model.Type == AutomatonType.EpsilonNFA && (targetType == AutomatonType.NFA || targetType == AutomatonType.DFA)) ||
            (model.Type == AutomatonType.NFA && targetType == AutomatonType.DFA);
        if (!allowed) return RedirectToAction("Index", "Home");

        AutomatonViewModel converted;
        List<string> warnings = new();
        try
        {
            if (targetType == AutomatonType.DFA)
            {
                // Domain conversion chain: (EpsilonNFA -> NFA -> DFA) or (NFA -> DFA)
                converted = conversionService.ConvertToDFA(model);
            }
            else // EpsilonNFA -> NFA
            {
                (converted, warnings) = conversionService.ConvertAutomatonType(model, targetType);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SwitchType conversion failed {From}->{To}; returning original", model.Type, targetType);
            return RedirectToAction("Index", "Home");
        }

        // Reset execution state but keep input so user can immediately run simulation on new type
        converted.ClearExecutionState(keepInput: true);
        // Store converted automaton in TempData and redirect to Home/Index so the UI pipeline and
        // integration tests observe the same behavior as before (Home controller will pick up TempData).
        converted.ClearExecutionState(keepInput: true);
        tempDataService.StoreCustomAutomaton(TempData, converted);
        foreach (var w in warnings) tempDataService.StoreConversionMessage(TempData, w);
        return RedirectToAction("Index", "Home");
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
