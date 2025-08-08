using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace FiniteAutomatons.Controllers;

public class HomeController(ILogger<HomeController> logger, IAutomatonTempDataService tempDataService, IHomeAutomatonService homeAutomatonService) : Controller
{
    private readonly ILogger<HomeController> logger = logger;
    private readonly IAutomatonTempDataService tempDataService = tempDataService;
    private readonly IHomeAutomatonService homeAutomatonService = homeAutomatonService;

    public IActionResult Index()
    {
        logger.LogInformation("Index action called");

        var (hasCustomAutomaton, customModel) = tempDataService.TryGetCustomAutomaton(TempData);
        
        if (hasCustomAutomaton && customModel != null)
        {
            return View(customModel);
        }

        var defaultModel = homeAutomatonService.GenerateDefaultAutomaton();
        return View(defaultModel);
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
}
