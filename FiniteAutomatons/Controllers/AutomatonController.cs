using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Core.Utilities;
using FiniteAutomatons.Services.Interfaces;
using FiniteAutomatons.Services.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace FiniteAutomatons.Controllers;

public class AutomatonController(
 ILogger<AutomatonController> logger,
 IAutomatonGeneratorService generatorService,
 IAutomatonTempDataService tempDataService,
 IAutomatonValidationService validationService,
 IAutomatonConversionService conversionService,
 IAutomatonExecutionService executionService,
 IAutomatonEditingService editingService,
 IAutomatonFileService fileService,
 ISavedAutomatonService? savedAutomatonService = null,
 UserManager<IdentityUser>? userManager = null,
 IRegexToAutomatonService? regexService = null) : Controller
{
    private readonly ILogger<AutomatonController> logger = logger;
    private readonly IAutomatonGeneratorService generatorService = generatorService;
    private readonly IAutomatonTempDataService tempDataService = tempDataService;
    private readonly IAutomatonValidationService validationService = validationService;
    private readonly IAutomatonConversionService conversionService = conversionService;
    private readonly IAutomatonExecutionService executionService = executionService;
    private readonly IAutomatonEditingService editingService = editingService;
    private readonly IAutomatonFileService fileService = fileService;
    private readonly ISavedAutomatonService? savedAutomatonService = savedAutomatonService;
    private readonly UserManager<IdentityUser>? userManager = userManager;
    private readonly IRegexToAutomatonService? regexService = regexService;

    // GET create page
    public IActionResult CreateAutomaton()
    {
        return View(new AutomatonViewModel());
    }

    // POST create
    [HttpPost]
    public IActionResult CreateAutomaton(AutomatonViewModel model)
    {
        logger.LogInformation("CreateAutomaton POST Type={Type} States={States} Transitions={Transitions}",
        model.Type, model.States.Count, model.Transitions.Count);

        var (isValid, errors) = validationService.ValidateAutomaton(model);
        if (!isValid)
        {
            foreach (var e in errors) ModelState.AddModelError(string.Empty, e);
            return View(model);
        }

        model.IsCustomAutomaton = true;
        tempDataService.StoreCustomAutomaton(TempData, model);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public IActionResult ChangeAutomatonType(AutomatonViewModel model, AutomatonType newType)
    {
        if (model.Type == newType) return View("CreateAutomaton", model);
        var (converted, warnings) = conversionService.ConvertAutomatonType(model, newType);
        foreach (var w in warnings) ModelState.AddModelError(string.Empty, w);
        converted.ClearExecutionState();
        return View("CreateAutomaton", converted);
    }

    [HttpPost]
    public IActionResult AddState(AutomatonViewModel model, int stateId, bool isStart, bool isAccepting)
    {
        var svc = editingService ?? new AutomatonEditingService(new AutomatonValidationService(NullLogger<AutomatonValidationService>.Instance), NullLogger<AutomatonEditingService>.Instance);
        model.States ??= [];
        model.Transitions ??= [];
        if (!ModelState.IsValid)
        {
            return View("CreateAutomaton", model);
        }
        var result = svc.AddState(model, stateId, isStart, isAccepting);
        if (!result.Ok && result.Error != null) ModelState.AddModelError(string.Empty, result.Error);
        return View("CreateAutomaton", model);
    }

    [HttpPost]
    public IActionResult RemoveState(AutomatonViewModel model, int stateId)
    {
        var svc = editingService ?? new AutomatonEditingService(new AutomatonValidationService(NullLogger<AutomatonValidationService>.Instance), NullLogger<AutomatonEditingService>.Instance);
        model.States ??= [];
        model.Transitions ??= [];
        if (!ModelState.IsValid)
        {
            return View("CreateAutomaton", model);
        }
        var result = svc.RemoveState(model, stateId);
        if (!result.Ok && result.Error != null) ModelState.AddModelError(string.Empty, result.Error);
        return View("CreateAutomaton", model);
    }

    [HttpPost]
    public IActionResult AddTransition(AutomatonViewModel model, int fromStateId, int toStateId, string symbol, string? newTransitionStackPop = null, string? newTransitionStackPush = null)
    {
        var svc = editingService ?? new AutomatonEditingService(new AutomatonValidationService(NullLogger<AutomatonValidationService>.Instance), NullLogger<AutomatonEditingService>.Instance);
        model.States ??= [];
        model.Transitions ??= [];
        if (!ModelState.IsValid)
        {
            return View("CreateAutomaton", model);
        }
        var result = svc.AddTransition(model, fromStateId, toStateId, symbol, newTransitionStackPop, newTransitionStackPush);
        if (!result.Ok && result.Error != null) ModelState.AddModelError(string.Empty, result.Error);
        return View("CreateAutomaton", model);
    }

    [HttpPost]
    public IActionResult RemoveTransition(AutomatonViewModel model, int fromStateId, int toStateId, string symbol)
    {
        var svc = editingService ?? new AutomatonEditingService(new AutomatonValidationService(NullLogger<AutomatonValidationService>.Instance), NullLogger<AutomatonEditingService>.Instance);
        model.States ??= [];
        model.Transitions ??= [];
        if (!ModelState.IsValid)
        {
            return View("CreateAutomaton", model);
        }
        var result = svc.RemoveTransition(model, fromStateId, toStateId, symbol);
        if (!result.Ok && result.Error != null) ModelState.AddModelError(string.Empty, result.Error);
        return View("CreateAutomaton", model);
    }

    [HttpPost]
    public IActionResult Start([FromForm] AutomatonViewModel model)
    {
        model.HasExecuted = true;
        var updated = executionService.BackToStart(model);
        updated.HasExecuted = true;
        ModelState.Clear(); // ensure updated values rendered
        return View("../Home/Index", updated);
    }

    [HttpPost]
    public IActionResult StepForward([FromForm] AutomatonViewModel model)
    {
        model.HasExecuted = true;
        var updated = executionService.ExecuteStepForward(model);
        updated.HasExecuted = true;
        ModelState.Clear(); // ensure updated values rendered
        return View("../Home/Index", updated);
    }

    [HttpPost]
    public IActionResult StepBackward([FromForm] AutomatonViewModel model)
    {
        var updated = executionService.ExecuteStepBackward(model);
        updated.HasExecuted = model.HasExecuted || updated.Position > 0;
        ModelState.Clear(); // ensure updated values rendered
        return View("../Home/Index", updated);
    }

    [HttpPost]
    public IActionResult ExecuteAll([FromForm] AutomatonViewModel model)
    {
        model.HasExecuted = true;
        var updated = executionService.ExecuteAll(model);
        updated.HasExecuted = true;
        ModelState.Clear(); // ensure updated values rendered
        return View("../Home/Index", updated);
    }

    [HttpPost]
    public IActionResult BackToStart([FromForm] AutomatonViewModel model)
    {
        var updated = executionService.BackToStart(model);
        updated.HasExecuted = model.HasExecuted || model.Position > 0 || model.Result != null;
        ModelState.Clear(); // ensure updated values rendered
        return View("../Home/Index", updated);
    }

    [HttpPost]
    public IActionResult Reset([FromForm] AutomatonViewModel model)
    {
        var updated = executionService.ResetExecution(model);
        updated.HasExecuted = false;
        ModelState.Clear(); // ensure updated values rendered
        return View("../Home/Index", updated);
    }

    [HttpPost]
    public IActionResult ConvertToDFA([FromForm] AutomatonViewModel model)
    {
        var converted = conversionService.ConvertToDFA(model);
        converted.ClearExecutionState();
        tempDataService.StoreCustomAutomaton(TempData, converted);
        tempDataService.StoreConversionMessage(TempData, $"Successfully converted {model.TypeDisplayName} to DFA with {converted.States.Count} states.");
        return RedirectToAction("Index", "Home");
    }

    // GET generator page
    public IActionResult GenerateRandomAutomaton()
    {
        return View(new RandomAutomatonGenerationViewModel
        {
            Type = AutomatonType.DFA,
            StateCount = 5,
            TransitionCount = 8,
            AlphabetSize = 3,
            AcceptingStateRatio = 0.3
        });
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
        var generated = generatorService.GenerateRandomAutomaton(model.Type, model.StateCount, model.TransitionCount, model.AlphabetSize, model.AcceptingStateRatio, model.Seed);
        tempDataService.StoreCustomAutomaton(TempData, generated);
        tempDataService.StoreConversionMessage(TempData, $"Successfully generated random {model.Type} with {generated.States.Count} states and {generated.Transitions.Count} transitions.");
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public IActionResult GenerateRealisticAutomaton(AutomatonType type, int stateCount, int? seed = null)
    {
        logger.LogInformation("Generating realistic automaton Type={Type} States={States}", type, stateCount);
        if (stateCount < 1 || stateCount > 20)
        {
            tempDataService.StoreErrorMessage(TempData, "State count must be between 1 and 20.");
            return RedirectToAction("GenerateRandomAutomaton");
        }
        var generated = generatorService.GenerateRealisticAutomaton(type, stateCount, seed);
        tempDataService.StoreCustomAutomaton(TempData, generated);
        tempDataService.StoreConversionMessage(TempData, $"Successfully generated realistic {type} with {generated.States.Count} states and {generated.Transitions.Count} transitions.");
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public async Task<IActionResult> ImportAutomaton(IFormFile upload)
    {
        if (upload == null)
        {
            ModelState.AddModelError(string.Empty, "No file uploaded.");
            return View("CreateAutomaton", new AutomatonViewModel());
        }
        var (ok, model, error) = await fileService.LoadFromFileAsync(upload);
        if (!ok || model == null)
        {
            ModelState.AddModelError(string.Empty, error ?? "Failed to load automaton.");
            return View("CreateAutomaton", new AutomatonViewModel());
        }
        tempDataService.StoreCustomAutomaton(TempData, model);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public IActionResult ExportJson([FromForm] AutomatonViewModel model)
    {
        var (name, content) = fileService.ExportJson(model);
        return File(System.Text.Encoding.UTF8.GetBytes(content), "application/json", name);
    }

    [HttpPost]
    public IActionResult ExportText([FromForm] AutomatonViewModel model)
    {
        var (name, content) = fileService.ExportText(model);
        return File(System.Text.Encoding.UTF8.GetBytes(content), "text/plain", name);
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
                States = [.. enfa.States.Select(s => new State { Id = s.Id, IsStart = s.IsStart, IsAccepting = s.IsAccepting })],
                Transitions = [.. enfa.Transitions.Select(t => new Transition { FromStateId = t.FromStateId, ToStateId = t.ToStateId, Symbol = t.Symbol })],
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

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> SavedAutomatons(int? groupId = null)
    {
        if (savedAutomatonService == null || userManager == null) return StatusCode(500);
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        var groups = await savedAutomatonService.ListGroupsForUserAsync(user.Id);
        var list = await savedAutomatonService.ListForUserAsync(user.Id, groupId);
        ViewData["Groups"] = groups;
        ViewData["SelectedGroupId"] = groupId;
        return View(list);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> CreateSavedGroup(string name, string? description)
    {
        if (savedAutomatonService == null || userManager == null) return StatusCode(500);
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("Name required");
        var g = await savedAutomatonService.CreateGroupAsync(user.Id, name.Trim(), string.IsNullOrWhiteSpace(description) ? null : description.Trim());
        return RedirectToAction("SavedAutomatons", new { groupId = g.Id });
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> DeleteSavedAutomaton(int id)
    {
        if (savedAutomatonService == null || userManager == null) return StatusCode(500);
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        await savedAutomatonService.DeleteAsync(id, user.Id);
        return RedirectToAction("SavedAutomatons");
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> SaveAutomaton([FromForm] AutomatonViewModel model, string name, string? description, bool saveState = false)
    {
        if (savedAutomatonService == null || userManager == null) return StatusCode(500);
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError(string.Empty, "Name is required to save automaton.");
            return View("CreateAutomaton", model);
        }

        _ = await savedAutomatonService.SaveAsync(user.Id, name.Trim(), string.IsNullOrWhiteSpace(description) ? null : description.Trim(), model, saveState);
        TempData["ConversionMessage"] = "Automaton saved successfully.";
        return RedirectToAction("SavedAutomatons");
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> LoadSavedAutomaton(int id, bool asState = false)
    {
        if (savedAutomatonService == null || userManager == null) return StatusCode(500);
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var entity = await savedAutomatonService.GetAsync(id, user.Id);
        if (entity == null) return NotFound();

        // Deserialize payload into AutomatonViewModel
        try
        {
            var payload = System.Text.Json.JsonSerializer.Deserialize<AutomatonPayloadDto>(entity.ContentJson);
            if (payload == null) return NotFound();
            var model = new AutomatonViewModel
            {
                Type = payload.Type,
                States = payload.States ?? [],
                Transitions = payload.Transitions ?? [],
                IsCustomAutomaton = true
            };

            if (asState && entity.HasExecutionState && !string.IsNullOrWhiteSpace(entity.ExecutionStateJson))
            {
                try
                {
                    var exec = System.Text.Json.JsonSerializer.Deserialize<SavedExecutionStateDto>(entity.ExecutionStateJson);
                    if (exec != null)
                    {
                        model.Input = exec.Input ?? string.Empty;
                        model.Position = exec.Position;
                        model.CurrentStateId = exec.CurrentStateId;
                        model.CurrentStates = exec.CurrentStates != null ? [.. exec.CurrentStates] : null;
                        model.IsAccepted = exec.IsAccepted;
                        model.StateHistorySerialized = exec.StateHistorySerialized ?? string.Empty;
                        model.StackSerialized = exec.StackSerialized;
                    }
                }
                catch { /* ignore execution state errors and load automaton only */ }
            }

            tempDataService.StoreCustomAutomaton(TempData, model);
            return RedirectToAction("Index", "Home");
        }
        catch
        {
            return StatusCode(500);
        }
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Groups()
    {
        if (savedAutomatonService == null || userManager == null) return StatusCode(500);
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        var groups = await savedAutomatonService.ListGroupsForUserAsync(user.Id);
        return View(groups);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> ManageGroup(int id)
    {
        if (savedAutomatonService == null || userManager == null) return StatusCode(500);
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        var grp = await savedAutomatonService.GetGroupAsync(id);
        if (grp == null) return NotFound();
        if (grp.UserId != user.Id) return Forbid(); // only owner can manage
        var members = await savedAutomatonService.ListGroupMembersAsync(id);
        ViewData["Group"] = grp;
        return View((grp, members));
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> AddGroupMember(int groupId, string memberUserId)
    {
        if (savedAutomatonService == null || userManager == null) return StatusCode(500);
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        var grp = await savedAutomatonService.GetGroupAsync(groupId);
        if (grp == null) return NotFound();
        if (grp.UserId != user.Id) return Forbid();
        if (string.IsNullOrWhiteSpace(memberUserId)) return BadRequest("memberUserId required");
        await savedAutomatonService.AddGroupMemberAsync(groupId, memberUserId.Trim());
        return RedirectToAction("ManageGroup", new { id = groupId });
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> RemoveGroupMember(int groupId, string memberUserId)
    {
        if (savedAutomatonService == null || userManager == null) return StatusCode(500);
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        var grp = await savedAutomatonService.GetGroupAsync(groupId);
        if (grp == null) return NotFound();
        if (grp.UserId != user.Id) return Forbid();
        await savedAutomatonService.RemoveGroupMemberAsync(groupId, memberUserId);
        return RedirectToAction("ManageGroup", new { id = groupId });
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> SetGroupSharingPolicy(int groupId, bool membersCanShare)
    {
        if (savedAutomatonService == null || userManager == null) return StatusCode(500);
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        var grp = await savedAutomatonService.GetGroupAsync(groupId);
        if (grp == null) return NotFound();
        if (grp.UserId != user.Id) return Forbid();
        await savedAutomatonService.SetGroupSharingPolicyAsync(groupId, membersCanShare);
        return RedirectToAction("ManageGroup", new { id = groupId });
    }

    private sealed class AutomatonPayloadDto
    {
        public AutomatonType Type { get; set; }
        public List<State>? States { get; set; }
        public List<Transition>? Transitions { get; set; }
    }

    private sealed class SavedExecutionStateDto
    {
        public string? Input { get; set; }
        public int Position { get; set; }
        public int? CurrentStateId { get; set; }
        public List<int>? CurrentStates { get; set; }
        public bool? IsAccepted { get; set; }
        public string? StateHistorySerialized { get; set; }
        public string? StackSerialized { get; set; }
    }
}
