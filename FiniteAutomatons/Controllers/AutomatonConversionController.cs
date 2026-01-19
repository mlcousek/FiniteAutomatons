using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Core.Utilities;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FiniteAutomatons.Controllers;

public class AutomatonConversionController(
    ILogger<AutomatonConversionController> logger,
    IAutomatonConversionService conversionService,
    IAutomatonTempDataService tempDataService) : Controller
{
    private readonly ILogger<AutomatonConversionController> logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IAutomatonConversionService conversionService = conversionService ?? throw new ArgumentNullException(nameof(conversionService));
    private readonly IAutomatonTempDataService tempDataService = tempDataService ?? throw new ArgumentNullException(nameof(tempDataService));

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
        List<string> warnings = [];
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

        converted.ClearExecutionState(keepInput: true);

        tempDataService.StoreCustomAutomaton(TempData, converted);
        foreach (var w in warnings) tempDataService.StoreConversionMessage(TempData, w);
        return RedirectToAction("Index", "Home");
    }
}
