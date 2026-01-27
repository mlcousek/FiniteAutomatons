using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace FiniteAutomatons.Controllers;

[Authorize]
public class SavedAutomatonController(
    ISavedAutomatonService savedAutomatonService,
    IAutomatonTempDataService tempDataService,
    UserManager<IdentityUser> userManager) : Controller
{
    private readonly ISavedAutomatonService savedAutomatonService = savedAutomatonService;
    private readonly IAutomatonTempDataService tempDataService = tempDataService;
    private readonly UserManager<IdentityUser> userManager = userManager;

    [HttpGet]
    public async Task<IActionResult> Index(int? groupId = null)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        var groups = await savedAutomatonService.ListGroupsForUserAsync(user.Id);
        var list = await savedAutomatonService.ListForUserAsync(user.Id, groupId);
        ViewData["Groups"] = groups;
        ViewData["SelectedGroupId"] = groupId;
        return View("SavedAutomatons", list);
    }

    [HttpPost]
    public async Task<IActionResult> CreateGroup(string name, string? description)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("Name required");
        try
        {
            var g = await savedAutomatonService.CreateGroupAsync(user.Id, name.Trim(), string.IsNullOrWhiteSpace(description) ? null : description.Trim());
            TempData["CreateGroupResult"] = "Group created.";
            TempData["CreateGroupSuccess"] = "1";
            return RedirectToAction("Index", new { groupId = g.Id });
        }
        catch
        {
            TempData["CreateGroupResult"] = "Group was not created.";
            TempData["CreateGroupSuccess"] = "0";
            return RedirectToAction("Index");
        }
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        await savedAutomatonService.DeleteAsync(id, user.Id);
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromForm] AutomatonViewModel model, string name, string? description, bool saveState = false)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError(string.Empty, "Name is required to save automaton.");
            return View("CreateAutomaton", model);
        }

        _ = await savedAutomatonService.SaveAsync(user.Id, name.Trim(), string.IsNullOrWhiteSpace(description) ? null : description.Trim(), model, saveState);
        TempData["ConversionMessage"] = "Automaton saved successfully.";
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> Load(int id, bool asState = false)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var entity = await savedAutomatonService.GetAsync(id, user.Id);
        if (entity == null) return NotFound();

        try
        {
            var payload = JsonSerializer.Deserialize<AutomatonPayloadDto>(entity.ContentJson);
            if (payload == null) return NotFound();
            var model = new AutomatonViewModel
            {
                Type = payload.Type,
                States = payload.States ?? new List<State>(),
                Transitions = payload.Transitions ?? new List<Transition>(),
                IsCustomAutomaton = true
            };

            if (asState && entity.HasExecutionState && !string.IsNullOrWhiteSpace(entity.ExecutionStateJson))
            {
                try
                {
                    var exec = JsonSerializer.Deserialize<SavedExecutionStateDto>(entity.ExecutionStateJson);
                    if (exec != null)
                    {
                        model.Input = exec.Input ?? string.Empty;
                        model.Position = exec.Position;
                        model.CurrentStateId = exec.CurrentStateId;
                        model.CurrentStates = exec.CurrentStates != null ? new HashSet<int>(exec.CurrentStates) : null;
                        model.IsAccepted = exec.IsAccepted;
                        model.StateHistorySerialized = exec.StateHistorySerialized ?? string.Empty;
                        model.StackSerialized = exec.StackSerialized;
                    }
                }
                catch { }
            }

            tempDataService.StoreCustomAutomaton(TempData, model);
            return RedirectToAction("Index", "Home");
        }
        catch
        {
            return StatusCode(500);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Groups()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        var groups = await savedAutomatonService.ListGroupsForUserAsync(user.Id);
        return View(groups);
    }

    [HttpGet]
    public async Task<IActionResult> ManageGroup(int id)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        var grp = await savedAutomatonService.GetGroupAsync(id);
        if (grp == null) return NotFound();
        if (grp.UserId != user.Id) return Forbid();
        var members = await savedAutomatonService.ListGroupMembersAsync(id);
        ViewData["Group"] = grp;
        return View((grp, members));
    }

    [HttpPost]
    public async Task<IActionResult> AddGroupMember(int groupId, string memberUserId)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        var grp = await savedAutomatonService.GetGroupAsync(groupId);
        if (grp == null) return NotFound();
        if (grp.UserId != user.Id) return Forbid();
        if (string.IsNullOrWhiteSpace(memberUserId)) return BadRequest("memberUserId required");
        await savedAutomatonService.AddGroupMemberAsync(groupId, memberUserId.Trim());
        return RedirectToAction("ManageGroup", new { id = groupId });
    }

    [HttpPost]
    public async Task<IActionResult> RemoveGroupMember(int groupId, string memberUserId)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        var grp = await savedAutomatonService.GetGroupAsync(groupId);
        if (grp == null) return NotFound();
        if (grp.UserId != user.Id) return Forbid();
        await savedAutomatonService.RemoveGroupMemberAsync(groupId, memberUserId);
        return RedirectToAction("ManageGroup", new { id = groupId });
    }

    [HttpPost]
    public async Task<IActionResult> SetGroupSharingPolicy(int groupId, bool membersCanShare)
    {
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
