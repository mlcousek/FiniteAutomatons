using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Core.Utilities;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FiniteAutomatons.Controllers;

public class RegexController : Controller
{
    private readonly ILogger<RegexController> logger;
    private readonly IRegexToAutomatonService? regexService;
    private readonly IAutomatonTempDataService tempDataService;

    public RegexController(ILogger<RegexController> logger, IAutomatonTempDataService tempDataService, IRegexToAutomatonService? regexService = null)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.tempDataService = tempDataService ?? throw new ArgumentNullException(nameof(tempDataService));
        this.regexService = regexService;
    }

    // GET: Regex to Automaton UI (development/testing helper)
    public IActionResult RegexToAutomaton()
    {
        // Note: the backend endpoint used by this UI is available only in Development environment
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult BuildFromRegex([FromForm] string regex)
    {
        if (string.IsNullOrWhiteSpace(regex))
        {
            return Json(new { success = false, error = "Empty regex provided" });
        }

        if (regexService == null)
        {
            logger.LogWarning("Attempt to build from regex but IRegexToAutomatonService is not available");
            return Json(new { success = false, error = "Service unavailable" });
        }

        try
        {
            var enfa = regexService.BuildEpsilonNfaFromRegex(regex.Trim());

            var model = new AutomatonViewModel
            {
                Type = AutomatonType.EpsilonNFA,
                States = enfa.States.Select(s => new State { Id = s.Id, IsStart = s.IsStart, IsAccepting = s.IsAccepting }).ToList(),
                Transitions = enfa.Transitions.Select(t => new Transition { FromStateId = t.FromStateId, ToStateId = t.ToStateId, Symbol = t.Symbol }).ToList(),
                IsCustomAutomaton = true,
                Input = string.Empty
            };

            model.NormalizeEpsilonTransitions();
            tempDataService.StoreCustomAutomaton(TempData, model);
            tempDataService.StoreConversionMessage(TempData, "Converted regex to automaton and loaded into simulator.");

            var redirect = Url.Action("Index", "Home");
            return Json(new { success = true, redirect });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to build automaton from regex via controller");
            return Json(new { success = false, error = ex.Message });
        }
    }
}
