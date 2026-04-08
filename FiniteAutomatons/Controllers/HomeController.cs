using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

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

        var (hasCustomAutomaton, customModel) = tempDataService.TryGetCustomAutomaton(TempData);
        if (hasCustomAutomaton && customModel != null)
        {
            model = customModel;
            logger.LogInformation("Loaded automaton from TempData");
        }
        else if (tempDataService.TryGetSessionAutomaton(HttpContext.Session, CanvasApiController.SessionKey) is var sessionResult && sessionResult.Success && sessionResult.Model != null)
        {
            model = sessionResult.Model;
            logger.LogInformation("Loaded automaton from Session (canvas edits)");
        }
        else
        {
            model = homeAutomatonService.GenerateDefaultAutomaton();
            logger.LogInformation("Loaded default automaton");
        }

        var analysis = minimizationService.AnalyzeAutomaton(model);
        ViewData["MinimizationAnalysis"] = analysis;


        if (TempData.TryGetValue("LayoutJson", out var layoutJson) && layoutJson is string layoutJsonStr && !string.IsNullOrWhiteSpace(layoutJsonStr))
        {
            ViewData["LayoutJson"] = layoutJsonStr;
        }

        return View(model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult Terms()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
