using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Core.Utilities;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using FiniteAutomatons.Core.Models.Database;

namespace FiniteAutomatons.Controllers;

[Route("[controller]/[action]")]
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
    public IActionResult CreateAutomaton() => View(new AutomatonViewModel());

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
        if (!ModelState.IsValid)
        {
            return View(model);
        }
        var (ok, err) = editingService.AddState(model, stateId, isStart, isAccepting);
        if (!ok) ModelState.AddModelError(string.Empty, err!);
        return View("CreateAutomaton", model);
    }

    [HttpPost]
    public IActionResult RemoveState(AutomatonViewModel model, int stateId)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }
        var (ok, err) = editingService.RemoveState(model, stateId);
        if (!ok) ModelState.AddModelError(string.Empty, err!);
        return View("CreateAutomaton", model);
    }

    [HttpPost]
    public IActionResult AddTransition(AutomatonViewModel model, int fromStateId, int toStateId, string symbol, string? newTransitionStackPop = null, string? newTransitionStackPush = null)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }
        var (ok, _, err) = editingService.AddTransition(model, fromStateId, toStateId, symbol, newTransitionStackPop, newTransitionStackPush);
        if (!ok) ModelState.AddModelError(string.Empty, err!);
        return View("CreateAutomaton", model);
    }

    [HttpPost]
    public IActionResult RemoveTransition(AutomatonViewModel model, int fromStateId, int toStateId, string symbol)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }
        var (ok, err) = editingService.RemoveTransition(model, fromStateId, toStateId, symbol);
        if (!ok) ModelState.AddModelError(string.Empty, err!);
        return View("CreateAutomaton", model);
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

    // rest of controller unchanged
}
