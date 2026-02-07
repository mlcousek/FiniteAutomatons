using FiniteAutomatons.Core.Models.Database;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Core.Utilities;
using FiniteAutomatons.Data;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace FiniteAutomatons.Controllers;

[Authorize]
public class SharedAutomatonController(
    ISharedAutomatonService sharedAutomatonService,
    ISharedAutomatonSharingService sharingService,
    IAutomatonTempDataService tempDataService,
    UserManager<IdentityUser> userManager,
    ApplicationDbContext context,
    ILogger<SharedAutomatonController> logger) : Controller
{
    private readonly ISharedAutomatonService sharedAutomatonService = sharedAutomatonService;
    private readonly ISharedAutomatonSharingService sharingService = sharingService;
    private readonly IAutomatonTempDataService tempDataService = tempDataService;
    private readonly UserManager<IdentityUser> userManager = userManager;
    private readonly ApplicationDbContext context = context;
    private readonly ILogger<SharedAutomatonController> logger = logger;

    #region View Actions

    [HttpGet]
    public async Task<IActionResult> Index(int? groupId)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        try
        {
            List<SharedAutomaton> automatons;
            List<SharedAutomatonGroup> groups = await sharedAutomatonService.ListGroupsForUserAsync(user.Id);

            if (groupId.HasValue)
            {
                automatons = await sharedAutomatonService.ListForGroupAsync(groupId.Value, user.Id);
                ViewData["SelectedGroupId"] = groupId.Value;
                
                // Get user role in this group
                var role = await sharedAutomatonService.GetUserRoleInGroupAsync(groupId.Value, user.Id);
                ViewData["UserRole"] = role;
            }
            else
            {
                automatons = await sharedAutomatonService.ListForUserAsync(user.Id);
            }

            ViewData["Groups"] = groups;
            return View(automatons);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "User {UserId} attempted unauthorized access", user.Id);
            TempData["Error"] = "You do not have permission to access this group.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet]
    public async Task<IActionResult> ManageMembers(int groupId)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        try
        {
            var group = await sharedAutomatonService.GetGroupAsync(groupId, user.Id);
            if (group == null)
                return NotFound();

            var members = await sharedAutomatonService.ListGroupMembersAsync(groupId, user.Id);
            var pendingInvitations = await sharingService.ListPendingInvitationsAsync(groupId, user.Id);
            var inviteLink = await sharingService.GetInviteLinkAsync(groupId, user.Id);
            var userRole = await sharedAutomatonService.GetUserRoleInGroupAsync(groupId, user.Id);

            // Fetch user emails for display
            var userEmails = new Dictionary<string, string>();
            foreach (var member in members)
            {
                var identityUser = await userManager.FindByIdAsync(member.UserId);
                userEmails[member.UserId] = identityUser?.Email ?? member.UserId;
            }

            ViewBag.Group = group;
            ViewBag.Members = members;
            ViewBag.PendingInvitations = pendingInvitations;
            ViewBag.InviteLink = inviteLink;
            ViewBag.UserRole = userRole;
            ViewBag.UserEmails = userEmails;

            return View();
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "User {UserId} unauthorized to manage members of group {GroupId}", user.Id, groupId);
            TempData["Error"] = "You do not have permission to manage members of this group.";
            return RedirectToAction(nameof(Index));
        }
    }

    #endregion

    #region Group Management

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateGroup(string name, string? description)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["CreateGroupResult"] = "Group name is required.";
            TempData["CreateGroupSuccess"] = "0";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var group = await sharedAutomatonService.CreateGroupAsync(user.Id, name, description);
            TempData["CreateGroupResult"] = $"Shared group '{group.Name}' created successfully!";
            TempData["CreateGroupSuccess"] = "1";
            return RedirectToAction(nameof(Index), new { groupId = group.Id });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating shared group for user {UserId}", user.Id);
            TempData["CreateGroupResult"] = "Failed to create group.";
            TempData["CreateGroupSuccess"] = "0";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteGroup(int id)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        try
        {
            await sharedAutomatonService.DeleteGroupAsync(id, user.Id);
            TempData["CreateGroupResult"] = "Group deleted successfully.";
            TempData["CreateGroupSuccess"] = "1";
            return RedirectToAction(nameof(Index));
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "User {UserId} unauthorized to delete group {GroupId}", user.Id, id);
            TempData["CreateGroupResult"] = "Only the group owner can delete the group.";
            TempData["CreateGroupSuccess"] = "0";
            return RedirectToAction(nameof(Index), new { groupId = id });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting group {GroupId}", id);
            TempData["CreateGroupResult"] = "Failed to delete group.";
            TempData["CreateGroupSuccess"] = "0";
            return RedirectToAction(nameof(Index), new { groupId = id });
        }
    }

    #endregion

    #region Automaton CRUD

    [HttpGet]
    public async Task<IActionResult> Load(int id, string mode = "structure")
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        try
        {
            var entity = await sharedAutomatonService.GetAsync(id, user.Id);
            if (entity == null) return NotFound();

            var payload = JsonSerializer.Deserialize<Core.Models.DTOs.AutomatonPayloadDto>(entity.ContentJson);
            if (payload == null) return NotFound();

            var model = new AutomatonViewModel
            {
                Type = payload.Type,
                States = payload.States ?? [],
                Transitions = payload.Transitions ?? [],
                IsCustomAutomaton = true
            };

            // Load execution state based on mode
            if ((mode == "input" || mode == "state") && !string.IsNullOrWhiteSpace(entity.ExecutionStateJson))
            {
                try
                {
                    var exec = JsonSerializer.Deserialize<Core.Models.DTOs.SavedExecutionStateDto>(entity.ExecutionStateJson);
                    if (exec != null)
                    {
                        model.Input = exec.Input ?? string.Empty;

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
                catch (JsonException ex)
                {
                    logger.LogWarning(ex, "Failed to parse execution state for shared automaton {Id}", id);
                }
            }

            model.NormalizeEpsilonTransitions();
            tempDataService.StoreCustomAutomaton(TempData, model);

            TempData["LoadMessage"] = $"Loaded shared automaton '{entity.Name}' from group";
            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading shared automaton {Id}", id);
            TempData["Error"] = "Failed to load automaton.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int groupId, string name, string? description, bool saveState = false)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["SaveError"] = "Name is required to save automaton.";
            return RedirectToAction(nameof(Index), new { groupId });
        }

        try
        {
            var (success, model) = tempDataService.TryGetCustomAutomaton(TempData);
            if (!success || model == null)
            {
                TempData["SaveError"] = "No automaton to save.";
                return RedirectToAction(nameof(Index), new { groupId });
            }

            await sharedAutomatonService.SaveAsync(user.Id, groupId, name.Trim(), description?.Trim(), model, saveState);
            TempData["CreateGroupResult"] = "Automaton saved to shared group successfully!";
            TempData["CreateGroupSuccess"] = "1";
            return RedirectToAction(nameof(Index), new { groupId });
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "User {UserId} unauthorized to save to group {GroupId}", user.Id, groupId);
            TempData["SaveError"] = "You do not have permission to add automatons to this group.";
            return RedirectToAction(nameof(Index), new { groupId });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving automaton to group {GroupId}", groupId);
            TempData["SaveError"] = "Failed to save automaton.";
            return RedirectToAction(nameof(Index), new { groupId });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        try
        {
            var automaton = await sharedAutomatonService.GetAsync(id, user.Id);
            if (automaton == null) return NotFound();

            var groupId = automaton.Assignments.FirstOrDefault()?.GroupId;

            await sharedAutomatonService.DeleteAsync(id, user.Id);
            TempData["CreateGroupResult"] = "Automaton deleted successfully.";
            TempData["CreateGroupSuccess"] = "1";

            if (groupId.HasValue)
                return RedirectToAction(nameof(Index), new { groupId });

            return RedirectToAction(nameof(Index));
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "User {UserId} unauthorized to delete automaton {Id}", user.Id, id);
            TempData["CreateGroupResult"] = "You do not have permission to delete this automaton.";
            TempData["CreateGroupSuccess"] = "0";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting automaton {Id}", id);
            TempData["CreateGroupResult"] = "Failed to delete automaton.";
            TempData["CreateGroupSuccess"] = "0";
            return RedirectToAction(nameof(Index));
        }
    }

    #endregion

    #region Member Management

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveMember(int groupId, string memberUserId)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        try
        {
            await sharedAutomatonService.RemoveMemberAsync(groupId, user.Id, memberUserId);
            TempData["MemberMessage"] = "Member removed successfully.";
            TempData["MemberSuccess"] = "1";
            return RedirectToAction(nameof(ManageMembers), new { groupId });
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "User {UserId} unauthorized to remove member from group {GroupId}", user.Id, groupId);
            TempData["MemberMessage"] = "You do not have permission to remove members.";
            TempData["MemberSuccess"] = "0";
            return RedirectToAction(nameof(ManageMembers), new { groupId });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error removing member from group {GroupId}", groupId);
            TempData["MemberMessage"] = ex.Message;
            TempData["MemberSuccess"] = "0";
            return RedirectToAction(nameof(ManageMembers), new { groupId });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateMemberRole(int groupId, string memberUserId, SharedGroupRole role)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        try
        {
            await sharedAutomatonService.UpdateMemberRoleAsync(groupId, user.Id, memberUserId, role);
            TempData["MemberMessage"] = "Member role updated successfully.";
            TempData["MemberSuccess"] = "1";
            return RedirectToAction(nameof(ManageMembers), new { groupId });
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "User {UserId} unauthorized to update roles in group {GroupId}", user.Id, groupId);
            TempData["MemberMessage"] = "You do not have permission to update member roles.";
            TempData["MemberSuccess"] = "0";
            return RedirectToAction(nameof(ManageMembers), new { groupId });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating member role in group {GroupId}", groupId);
            TempData["MemberMessage"] = ex.Message;
            TempData["MemberSuccess"] = "0";
            return RedirectToAction(nameof(ManageMembers), new { groupId });
        }
    }

    #endregion

    #region Sharing - Email Invitations

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InviteByEmail(int groupId, string email, SharedGroupRole role)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        try
        {
            var invitation = await sharingService.InviteByEmailAsync(groupId, user.Id, email, role);
            
            // TODO: Send actual email (for now just log)
            var emailContent = await sharingService.GetInvitationEmailContentAsync(invitation);
            logger.LogInformation("Email invitation created for {Email} to group {GroupId}. Token: {Token}",
                email, groupId, invitation.Token);

            TempData["MemberMessage"] = $"Invitation sent to {email}";
            TempData["MemberSuccess"] = "1";
            return RedirectToAction(nameof(ManageMembers), new { groupId });
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "User {UserId} unauthorized to invite to group {GroupId}", user.Id, groupId);
            TempData["MemberMessage"] = "You do not have permission to invite members.";
            TempData["MemberSuccess"] = "0";
            return RedirectToAction(nameof(ManageMembers), new { groupId });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error inviting email to group {GroupId}", groupId);
            TempData["MemberMessage"] = ex.Message;
            TempData["MemberSuccess"] = "0";
            return RedirectToAction(nameof(ManageMembers), new { groupId });
        }
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> AcceptInvitation(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest("Invalid invitation token");

        var invitation = await sharingService.GetInvitationByTokenAsync(token);
        if (invitation == null)
            return NotFound("Invitation not found");

        ViewBag.Invitation = invitation;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptInvitationConfirm(string token)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        try
        {
            await sharingService.AcceptInvitationAsync(token, user.Id);
            TempData["CreateGroupResult"] = "You have successfully joined the shared group!";
            TempData["CreateGroupSuccess"] = "1";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error accepting invitation for user {UserId}", user.Id);
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(AcceptInvitation), new { token });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeclineInvitation(string token)
    {
        try
        {
            await sharingService.DeclineInvitationAsync(token);
            TempData["Message"] = "Invitation declined.";
            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error declining invitation");
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(AcceptInvitation), new { token });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelInvitation(int groupId, int invitationId)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        try
        {
            await sharingService.CancelInvitationAsync(invitationId, user.Id);
            TempData["MemberMessage"] = "Invitation cancelled.";
            TempData["MemberSuccess"] = "1";
            return RedirectToAction(nameof(ManageMembers), new { groupId });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error cancelling invitation {InvitationId}", invitationId);
            TempData["MemberMessage"] = ex.Message;
            TempData["MemberSuccess"] = "0";
            return RedirectToAction(nameof(ManageMembers), new { groupId });
        }
    }

    #endregion

    #region Sharing - Invite Links

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateInviteLink(int groupId, SharedGroupRole defaultRole, int? expirationDays)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        try
        {
            var inviteCode = await sharingService.GenerateInviteLinkAsync(groupId, user.Id, defaultRole, expirationDays);
            var inviteUrl = Url.Action(nameof(JoinViaLink), "SharedAutomaton", new { code = inviteCode }, Request.Scheme);

            TempData["MemberMessage"] = $"Invite link generated: {inviteUrl}";
            TempData["MemberSuccess"] = "1";
            return RedirectToAction(nameof(ManageMembers), new { groupId });
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "User {UserId} unauthorized to generate link for group {GroupId}", user.Id, groupId);
            TempData["MemberMessage"] = "You do not have permission to generate invite links.";
            TempData["MemberSuccess"] = "0";
            return RedirectToAction(nameof(ManageMembers), new { groupId });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating invite link for group {GroupId}", groupId);
            TempData["MemberMessage"] = ex.Message;
            TempData["MemberSuccess"] = "0";
            return RedirectToAction(nameof(ManageMembers), new { groupId });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateInviteLink(int groupId)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        try
        {
            await sharingService.DeactivateInviteLinkAsync(groupId, user.Id);
            TempData["MemberMessage"] = "Invite link deactivated.";
            TempData["MemberSuccess"] = "1";
            return RedirectToAction(nameof(ManageMembers), new { groupId });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deactivating link for group {GroupId}", groupId);
            TempData["MemberMessage"] = ex.Message;
            TempData["MemberSuccess"] = "0";
            return RedirectToAction(nameof(ManageMembers), new { groupId });
        }
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> JoinViaLink(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest("Invalid invite code");

        // Check if user is authenticated first
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            // Store code in TempData and redirect to login
            TempData["InviteCode"] = code;
            return Challenge();
        }

        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        try
        {
            // Get group info to show confirmation page
            var group = await context.SharedAutomatonGroups
                .FirstOrDefaultAsync(g => g.InviteCode == code);

            if (group == null)
            {
                TempData["Error"] = "Invalid invite code";
                return RedirectToAction("Index", "Home");
            }

            ViewBag.Group = group;
            ViewBag.InviteCode = code;
            return View();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error with invite code {Code}", code);
            TempData["Error"] = ex.Message;
            return RedirectToAction("Index", "Home");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> JoinViaLinkConfirm(string code)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        try
        {
            var group = await sharingService.JoinViaInviteLinkAsync(code, user.Id);
            if (group != null)
            {
                TempData["CreateGroupResult"] = $"You have successfully joined '{group.Name}'!";
                TempData["CreateGroupSuccess"] = "1";
                return RedirectToAction(nameof(Index), new { groupId = group.Id });
            }
            
            TempData["Error"] = "Failed to join group";
            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error joining via link for user {UserId}", user.Id);
            TempData["Error"] = ex.Message;
            return RedirectToAction("Index", "Home");
        }
    }

    #endregion
}
