using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Core.Utilities;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FiniteAutomatons.Controllers;

public class RegexController : Controller
{
    private readonly ILogger<RegexController> logger;
    private readonly IRegexToAutomatonService regexService;
    private readonly IAutomatonTempDataService tempDataService;
    private readonly IRegexPresetService presetService;

    public RegexController(
        ILogger<RegexController> logger,
        IAutomatonTempDataService tempDataService,
        IRegexToAutomatonService regexService,
        IRegexPresetService presetService)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(tempDataService);
        ArgumentNullException.ThrowIfNull(regexService);
        ArgumentNullException.ThrowIfNull(presetService);

        this.logger = logger;
        this.tempDataService = tempDataService;
        this.regexService = regexService;
        this.presetService = presetService;
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

        try
        {
            var enfa = regexService.BuildEpsilonNfaFromRegex(regex.Trim());

            var model = new AutomatonViewModel
            {
                Type = AutomatonType.EpsilonNFA,
                States = enfa.States.Select(s => new State { Id = s.Id, IsStart = s.IsStart, IsAccepting = s.IsAccepting }).ToList(),
                Transitions = enfa.Transitions.Select(t => new Transition { FromStateId = t.FromStateId, ToStateId = t.ToStateId, Symbol = t.Symbol }).ToList(),
                IsCustomAutomaton = true,
                Input = string.Empty,
                SourceRegex = regex.Trim()
            };

            model.NormalizeEpsilonTransitions();
            tempDataService.StoreCustomAutomaton(TempData, model);
            tempDataService.StoreConversionMessage(TempData, "Converted regex to automaton and loaded into simulator.");

            var redirect = Url.Action("Index", "Home");
            return Json(new { success = true, redirect });
        }
        catch (ArgumentException ex) when (ex.Message.Contains("not supported"))
        {
            logger.LogWarning(ex, "Unsupported regex feature");
            return Json(new
            {
                success = false,
                error = $"{ex.Message}. Supported: literals, [char-class], ranges [a-z], *, +, ?, |, ()."
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to build automaton from regex via controller");
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult BuildFromPreset([FromForm] string presetKey)
    {
        if (string.IsNullOrWhiteSpace(presetKey))
        {
            return Json(new { success = false, error = "No preset selected" });
        }

        var preset = presetService.GetPresetByKey(presetKey);
        if (preset == null)
        {
            return Json(new { success = false, error = "Invalid preset" });
        }

        return BuildFromRegex(preset.Pattern);
    }

    [HttpGet]
    public IActionResult GetPresets()
    {
        var presets = presetService.GetAllPresets()
            .Select(p => new
            {
                key = p.Key,
                displayName = p.DisplayName,
                pattern = p.Pattern,
                description = p.Description,
                acceptExamples = p.AcceptExamples,
                rejectExamples = p.RejectExamples
            });

        return Json(presets);
    }
}
