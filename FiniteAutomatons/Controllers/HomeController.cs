using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text.Json;

namespace FiniteAutomatons.Controllers;

public class HomeController(ILogger<HomeController> logger, IAutomatonTempDataService tempDataService, IHomeAutomatonService homeAutomatonService, IAutomatonMinimizationService minimizationService) : Controller
{
    private readonly ILogger<HomeController> logger = logger;
    private readonly IAutomatonTempDataService tempDataService = tempDataService;
    private readonly IHomeAutomatonService homeAutomatonService = homeAutomatonService;
    private readonly IAutomatonMinimizationService minimizationService = minimizationService;

    public IActionResult Index()
    {
        logger.LogInformation("Index action called");

        AutomatonViewModel model;

        // Priority 1: TempData (set by server-side actions: convert, minimize, generate input…)
        //             TempData wins over session because it carries the result of a deliberate server action.
        var (hasCustomAutomaton, customModel) = tempDataService.TryGetCustomAutomaton(TempData);
        if (hasCustomAutomaton && customModel != null)
        {
            model = customModel;
            logger.LogInformation("Loaded automaton from TempData");
        }
        // Priority 2: Session — automaton saved by canvas editor via /api/canvas/save
        else if (TryGetSessionAutomaton(out var sessionModel) && sessionModel != null)
        {
            model = sessionModel;
            logger.LogInformation("Loaded automaton from Session (canvas edits)");
        }
        // Priority 3: Default
        else
        {
            model = homeAutomatonService.GenerateDefaultAutomaton();
            logger.LogInformation("Loaded default automaton");
        }

        var analysis = minimizationService.AnalyzeAutomaton(model);
        ViewData["MinimizationAnalysis"] = analysis;

        return View(model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    // ──────────────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Try to deserialize an AutomatonViewModel from the canvas Session key.
    /// Does NOT remove the key — canvas edits persist until a new automaton is loaded.
    /// </summary>
    private bool TryGetSessionAutomaton(out AutomatonViewModel? model)
    {
        model = null;
        var json = HttpContext.Session.GetString(CanvasApiController.SessionKey);
        if (string.IsNullOrEmpty(json)) return false;

        try
        {
            model = JsonSerializer.Deserialize<AutomatonViewModel>(json);
            if (model != null)
            {
                model.IsCustomAutomaton = true;
                model.States ??= [];
                model.Transitions ??= [];
            }
            return model != null;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to deserialize canvas automaton from Session");
            return false;
        }
    }
}
