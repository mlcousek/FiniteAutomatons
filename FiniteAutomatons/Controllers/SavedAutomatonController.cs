using FiniteAutomatons.Core.Models.Database;
using FiniteAutomatons.Core.Models.DTOs;
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
    IAutomatonFileService fileService,
    UserManager<ApplicationUser> userManager) : Controller
{
    private readonly ISavedAutomatonService savedAutomatonService = savedAutomatonService;
    private readonly IAutomatonTempDataService tempDataService = tempDataService;
    private readonly IAutomatonFileService fileService = fileService;
    private readonly UserManager<ApplicationUser> userManager = userManager;

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
    public async Task<IActionResult> RemoveFromGroup(int automatonId)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        try
        {
            await savedAutomatonService.AssignAutomatonToGroupAsync(automatonId, user.Id, null);
            TempData["CreateGroupResult"] = "Automaton removed from group.";
            TempData["CreateGroupSuccess"] = "1";
        }
        catch
        {
            TempData["CreateGroupResult"] = "Automaton was not removed from group.";
            TempData["CreateGroupSuccess"] = "0";
        }
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> DeleteGroup(int id)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        try
        {
            await savedAutomatonService.DeleteGroupAsync(id, user.Id);
            TempData["CreateGroupResult"] = "Group deleted.";
            TempData["CreateGroupSuccess"] = "1";
        }
        catch
        {
            TempData["CreateGroupResult"] = "Group was not deleted.";
            TempData["CreateGroupSuccess"] = "0";
        }
        return RedirectToAction("Index");
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
    public async Task<IActionResult> AssignToGroup(int automatonId, int? groupId)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        try
        {
            await savedAutomatonService.AssignAutomatonToGroupAsync(automatonId, user.Id, groupId);
            TempData["CreateGroupResult"] = groupId.HasValue ? "Automaton added to group." : "Automaton removed from group.";
            TempData["CreateGroupSuccess"] = "1";
        }
        catch
        {
            TempData["CreateGroupResult"] = "Automaton was not added to group.";
            TempData["CreateGroupSuccess"] = "0";
        }

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
    public async Task<IActionResult> Load(int id, string mode = "structure")
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
                States = payload.States ?? [],
                Transitions = payload.Transitions ?? [],
                IsCustomAutomaton = true,
                SourceRegex = entity.SourceRegex
            };

            // Load based on mode
            if ((mode == "input" || mode == "state") && !string.IsNullOrWhiteSpace(entity.ExecutionStateJson))
            {
                try
                {
                    var exec = JsonSerializer.Deserialize<SavedExecutionStateDto>(entity.ExecutionStateJson);
                    if (exec != null)
                    {
                        // Always load input for both modes
                        model.Input = exec.Input ?? string.Empty;

                        // Load execution state only for "state" mode
                        if (mode == "state" && entity.SaveMode == AutomatonSaveMode.WithState)
                        {
                            model.Position = exec.Position;
                            model.CurrentStateId = exec.CurrentStateId;
                            model.CurrentStates = exec.CurrentStates != null ? [.. exec.CurrentStates] : null;
                            model.IsAccepted = exec.IsAccepted;
                            model.StateHistorySerialized = exec.StateHistorySerialized ?? string.Empty;
                            model.StackSerialized = exec.StackSerialized;
                        }
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

    [HttpGet]
    public async Task<IActionResult> ExportGroup(int groupId)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var group = await savedAutomatonService.GetGroupAsync(groupId);
        if (group == null) return NotFound();

        var automatons = await savedAutomatonService.ListForUserAsync(user.Id, groupId);
        if (automatons == null || automatons.Count == 0)
        {
            TempData["CreateGroupResult"] = "No automatons in this group to export.";
            TempData["CreateGroupSuccess"] = "0";
            return RedirectToAction("Index", new { groupId });
        }

        var (fileName, content) = fileService.ExportGroup(group.Name, group.Description, automatons);

        return File(System.Text.Encoding.UTF8.GetBytes(content), "application/json", fileName);
    }

    [HttpPost]
    public async Task<IActionResult> ImportGroup(int groupId, IFormFile file)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var group = await savedAutomatonService.GetGroupAsync(groupId);
        if (group == null) return NotFound();

        var (ok, importData, error) = await fileService.ImportGroupAsync(file);

        if (!ok || importData == null)
        {
            TempData["CreateGroupResult"] = error ?? "Failed to import group.";
            TempData["CreateGroupSuccess"] = "0";
            return RedirectToAction("Index", new { groupId });
        }

        var importedCount = 0;
        var failedCount = 0;

        // Use a fixed timestamp for all imports to preserve order
        var baseTimestamp = DateTime.UtcNow;

        foreach (var auto in importData.Automatons)
        {
            try
            {
                var model = new AutomatonViewModel
                {
                    Type = auto.Content?.Type ?? AutomatonType.DFA,
                    States = auto.Content?.States ?? [],
                    Transitions = auto.Content?.Transitions ?? [],
                    IsCustomAutomaton = true
                };

                // Determine save mode and populate model based on execution state
                bool saveExecutionState = false;

                if (auto.ExecutionState != null)
                {
                    // Populate input
                    model.Input = auto.ExecutionState.Input ?? string.Empty;

                    if (auto.HasExecutionState)
                    {
                        // Full execution state
                        saveExecutionState = true;
                        model.Position = auto.ExecutionState.Position;
                        model.CurrentStateId = auto.ExecutionState.CurrentStateId;
                        model.CurrentStates = auto.ExecutionState.CurrentStates != null ? [.. auto.ExecutionState.CurrentStates] : null;
                        model.IsAccepted = auto.ExecutionState.IsAccepted;
                        model.StateHistorySerialized = auto.ExecutionState.StateHistorySerialized ?? string.Empty;
                        model.StackSerialized = auto.ExecutionState.StackSerialized;
                    }
                }

                var saved = await savedAutomatonService.SaveAsync(
                    user.Id,
                    auto.Name ?? "Imported",
                    auto.Description,
                    model,
                    saveExecutionState,
                    groupId);

                // Adjust CreatedAt to preserve import order
                // Note: This requires direct database access or a new service method
                // For now, we'll add a small delay to ensure order
                await Task.Delay(10);

                importedCount++;
            }
            catch
            {
                failedCount++;
            }
        }

        if (importedCount > 0)
        {
            var message = failedCount > 0
                ? $"Imported {importedCount} automaton(s) into group. {failedCount} failed."
                : $"Imported {importedCount} automaton(s) into group.";
            TempData["CreateGroupResult"] = message;
            TempData["CreateGroupSuccess"] = "1";
        }
        else
        {
            TempData["CreateGroupResult"] = "Failed to import any automatons.";
            TempData["CreateGroupSuccess"] = "0";
        }

        return RedirectToAction("Index", new { groupId });
    }
}


