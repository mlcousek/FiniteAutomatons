using FiniteAutomatons.Core.Models.Database;
using FiniteAutomatons.Core.Models.DTOs;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Data;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FiniteAutomatons.Services.Services;

public class SharedAutomatonService(
    ApplicationDbContext context,
    ILogger<SharedAutomatonService> logger) : ISharedAutomatonService
{
    private readonly ApplicationDbContext context = context;
    private readonly ILogger<SharedAutomatonService> logger = logger;

    public async Task<SharedAutomaton> SaveAsync(string userId, int groupId, string name, string? description, AutomatonViewModel model, bool saveExecutionState = false, string? layoutJson = null, string? thumbnailBase64 = null)
    {
        ValidateSaveParameters(userId, name, model);

        await ValidateUserCanAddToGroupAsync(groupId, userId);

        var payload = SerializeAutomatonPayload(model);
        var (saveMode, execJson) = DetermineExecutionState(model, saveExecutionState);
        var automaton = CreateSharedAutomatonEntity(userId, name, description, model, payload, saveMode, execJson, layoutJson, thumbnailBase64);

        context.SharedAutomatons.Add(automaton);
        await context.SaveChangesAsync();

        await CreateGroupAssignmentAsync(automaton.Id, groupId);

        LogSaveSuccess(userId, automaton.Id, name, groupId);
        return automaton;
    }

    private static void ValidateSaveParameters(string userId, string name, AutomatonViewModel model)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(model);
    }

    private async Task ValidateUserCanAddToGroupAsync(int groupId, string userId)
    {
        if (!await CanUserAddToGroupAsync(groupId, userId))
        {
            throw new UnauthorizedAccessException($"User {userId} does not have permission to add automatons to group {groupId}");
        }
    }

    private static string SerializeAutomatonPayload(AutomatonViewModel model)
    {
        var payload = new AutomatonPayloadDto
        {
            Type = model.Type,
            States = model.States,
            Transitions = model.Transitions
        };
        return JsonSerializer.Serialize(payload);
    }

    private static (AutomatonSaveMode SaveMode, string? ExecJson) DetermineExecutionState(AutomatonViewModel model, bool saveExecutionState)
    {
        if (saveExecutionState && !string.IsNullOrWhiteSpace(model.Input))
        {
            return (AutomatonSaveMode.WithState, SerializeFullExecutionState(model));
        }

        if (!string.IsNullOrWhiteSpace(model.Input))
        {
            return (AutomatonSaveMode.WithInput, SerializeInputOnly(model));
        }

        return (AutomatonSaveMode.Structure, null);
    }

    private static string SerializeFullExecutionState(AutomatonViewModel model)
    {
        var execState = new SavedExecutionStateDto
        {
            Input = model.Input,
            Position = model.Position,
            CurrentStateId = model.CurrentStateId,
            CurrentStates = model.CurrentStates != null ? [.. model.CurrentStates] : null,
            IsAccepted = model.IsAccepted,
            StateHistorySerialized = model.StateHistorySerialized ?? string.Empty,
            StackSerialized = model.StackSerialized
        };
        return JsonSerializer.Serialize(execState);
    }

    private static string SerializeInputOnly(AutomatonViewModel model)
    {
        var execState = new SavedExecutionStateDto
        {
            Input = model.Input,
            Position = 0,
            CurrentStateId = null,
            CurrentStates = null,
            IsAccepted = null,
            StateHistorySerialized = string.Empty,
            StackSerialized = null
        };
        return JsonSerializer.Serialize(execState);
    }

    private static SharedAutomaton CreateSharedAutomatonEntity(string userId, string name, string? description, 
        AutomatonViewModel model, string contentJson, AutomatonSaveMode saveMode, string? execJson, 
        string? layoutJson, string? thumbnailBase64)
    {
        return new SharedAutomaton
        {
            CreatedByUserId = userId,
            Name = name.Trim(),
            Description = description?.Trim(),
            ContentJson = contentJson,
            SaveMode = saveMode,
            ExecutionStateJson = execJson,
            LayoutJson = string.IsNullOrWhiteSpace(layoutJson) ? null : layoutJson,
            ThumbnailBase64 = string.IsNullOrWhiteSpace(thumbnailBase64) ? null : thumbnailBase64,
            SourceRegex = model.SourceRegex,
            CreatedAt = DateTime.UtcNow
        };
    }

    private async Task CreateGroupAssignmentAsync(int automatonId, int groupId)
    {
        var assignment = new SharedAutomatonGroupAssignment
        {
            AutomatonId = automatonId,
            GroupId = groupId,
            AssignedAt = DateTime.UtcNow
        };

        context.SharedAutomatonGroupAssignments.Add(assignment);
        await context.SaveChangesAsync();
    }

    private void LogSaveSuccess(string userId, int automatonId, string name, int groupId)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("User {UserId} saved shared automaton {AutomatonId} '{Name}' to group {GroupId}",
                userId, automatonId, name, groupId);
        }
    }

    public async Task<SharedAutomaton?> GetAsync(int id, string userId)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var automaton = await context.SharedAutomatons
            .Include(a => a.Assignments)
                .ThenInclude(a => a.Group)
                    .ThenInclude(g => g!.Members)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (automaton == null)
            return null;

        var hasAccess = automaton.Assignments.Any(assignment =>
            assignment.Group != null &&
            assignment.Group.Members.Any(m => m.UserId == userId));

        return hasAccess ? automaton : null;
    }

    public async Task<List<SharedAutomaton>> ListForGroupAsync(int groupId, string userId)
    {
        ArgumentNullException.ThrowIfNull(userId);

        if (!await CanUserViewGroupAsync(groupId, userId))
        {
            throw new UnauthorizedAccessException($"User {userId} does not have permission to view group {groupId}");
        }

        return await context.SharedAutomatons
            .Include(a => a.Assignments)
            .Where(a => a.Assignments.Any(assignment => assignment.GroupId == groupId))
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<SharedAutomaton>> ListForUserAsync(string userId)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var userGroupIds = await context.SharedAutomatonGroupMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.GroupId)
            .ToListAsync();

        return await context.SharedAutomatons
            .Include(a => a.Assignments)
                .ThenInclude(a => a.Group)
            .Where(a => a.Assignments.Any(assignment => userGroupIds.Contains(assignment.GroupId)))
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task DeleteAsync(int id, string userId)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var automaton = await LoadAutomatonWithPermissionsAsync(id);
        await ValidateDeletePermissionAsync(automaton, userId, id);

        context.SharedAutomatons.Remove(automaton);
        await context.SaveChangesAsync();

        LogDeleteSuccess(userId, id);
    }

    private async Task<SharedAutomaton> LoadAutomatonWithPermissionsAsync(int id)
    {
        return await context.SharedAutomatons
            .Include(a => a.Assignments)
                .ThenInclude(a => a.Group)
                    .ThenInclude(g => g!.Members)
            .FirstOrDefaultAsync(a => a.Id == id) 
            ?? throw new InvalidOperationException($"Automaton {id} not found");
    }

    private async Task ValidateDeletePermissionAsync(SharedAutomaton automaton, string userId, int id)
    {
        if (await CanUserDeleteAutomatonAsync(automaton, userId))
            return;

        throw new UnauthorizedAccessException($"User {userId} does not have permission to delete automaton {id}");
    }

    private async Task<bool> CanUserDeleteAutomatonAsync(SharedAutomaton automaton, string userId)
    {
        if (automaton.CreatedByUserId == userId)
            return true;

        foreach (var assignment in automaton.Assignments)
        {
            var userRole = await GetUserRoleInGroupAsync(assignment.GroupId, userId);
            if (userRole >= SharedGroupRole.Editor)
                return true;
        }

        return false;
    }

    private void LogDeleteSuccess(string userId, int automatonId)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("User {UserId} deleted shared automaton {AutomatonId}", userId, automatonId);
        }
    }

    public async Task<SharedAutomaton> UpdateAsync(int id, string userId, string? name, string? description, AutomatonViewModel? model)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var automaton = await LoadAutomatonWithPermissionsAsync(id);
        await ValidateEditPermissionAsync(automaton, userId, id);

        UpdateAutomatonProperties(automaton, name, description, model, userId);

        await context.SaveChangesAsync();
        LogUpdateSuccess(userId, id);

        return automaton;
    }

    private async Task ValidateEditPermissionAsync(SharedAutomaton automaton, string userId, int id)
    {
        if (await CanUserEditAutomatonAsync(automaton, userId))
            return;

        throw new UnauthorizedAccessException($"User {userId} does not have permission to edit automaton {id}");
    }

    private async Task<bool> CanUserEditAutomatonAsync(SharedAutomaton automaton, string userId)
    {
        if (automaton.CreatedByUserId == userId)
            return true;

        foreach (var assignment in automaton.Assignments)
        {
            var userRole = await GetUserRoleInGroupAsync(assignment.GroupId, userId);
            if (userRole >= SharedGroupRole.Editor)
                return true;
        }

        return false;
    }

    private static void UpdateAutomatonProperties(SharedAutomaton automaton, string? name, string? description, AutomatonViewModel? model, string userId)
    {
        if (!string.IsNullOrWhiteSpace(name))
            automaton.Name = name.Trim();

        if (description != null)
            automaton.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        if (model != null)
        {
            var payload = new AutomatonPayloadDto
            {
                Type = model.Type,
                States = model.States,
                Transitions = model.Transitions
            };
            automaton.ContentJson = JsonSerializer.Serialize(payload);
        }

        automaton.ModifiedAt = DateTime.UtcNow;
        automaton.ModifiedByUserId = userId;
    }

    private void LogUpdateSuccess(string userId, int automatonId)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("User {UserId} updated shared automaton {AutomatonId}", userId, automatonId);
        }
    }

    public async Task<SharedAutomatonGroup> CreateGroupAsync(string userId, string name, string? description)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(name);

        var group = CreateGroupEntity(userId, name, description);
        context.SharedAutomatonGroups.Add(group);
        await context.SaveChangesAsync();

        await AddGroupOwnerAsync(group.Id, userId);
        LogGroupCreation(userId, group.Id, name);

        return group;
    }

    private static SharedAutomatonGroup CreateGroupEntity(string userId, string name, string? description)
    {
        return new SharedAutomatonGroup
        {
            UserId = userId,
            Name = name.Trim(),
            Description = description?.Trim(),
            CreatedAt = DateTime.UtcNow
        };
    }

    private async Task AddGroupOwnerAsync(int groupId, string userId)
    {
        var member = new SharedAutomatonGroupMember
        {
            GroupId = groupId,
            UserId = userId,
            Role = SharedGroupRole.Owner,
            JoinedAt = DateTime.UtcNow
        };

        context.SharedAutomatonGroupMembers.Add(member);
        await context.SaveChangesAsync();
    }

    private void LogGroupCreation(string userId, int groupId, string name)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("User {UserId} created shared group {GroupId} '{Name}'", userId, groupId, name);
        }
    }

    public async Task<SharedAutomatonGroup?> GetGroupAsync(int groupId, string userId)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var group = await context.SharedAutomatonGroups
            .Include(g => g.Members)
            .Include(g => g.Assignments)
            .FirstOrDefaultAsync(g => g.Id == groupId);

        if (group == null)
            return null;

        var isMember = group.Members.Any(m => m.UserId == userId);

        return isMember ? group : null;
    }

    public async Task<List<SharedAutomatonGroup>> ListGroupsForUserAsync(string userId)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await context.SharedAutomatonGroups
            .Include(g => g.Members)
            .Where(g => g.Members.Any(m => m.UserId == userId))
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync();
    }

    public async Task DeleteGroupAsync(int groupId, string userId)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var group = await context.SharedAutomatonGroups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == groupId) ?? throw new InvalidOperationException($"Group {groupId} not found");

        if (group.UserId != userId)
        {
            throw new UnauthorizedAccessException($"User {userId} is not the owner of group {groupId}");
        }

        context.SharedAutomatonGroups.Remove(group);
        await context.SaveChangesAsync();
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("User {UserId} deleted shared group {GroupId}", userId, groupId);
        }
    }

    public async Task UpdateGroupAsync(int groupId, string userId, string? name, string? description)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var group = await context.SharedAutomatonGroups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == groupId) ?? throw new InvalidOperationException($"Group {groupId} not found");

        if (!await CanUserManageMembersAsync(groupId, userId))
        {
            throw new UnauthorizedAccessException($"User {userId} does not have permission to update group {groupId}");
        }

        if (!string.IsNullOrWhiteSpace(name))
            group.Name = name.Trim();

        if (description != null)
            group.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        await context.SaveChangesAsync();

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("User {UserId} updated shared group {GroupId}", userId, groupId);
        }
    }

    public async Task<List<SharedAutomatonGroupMember>> ListGroupMembersAsync(int groupId, string userId)
    {
        ArgumentNullException.ThrowIfNull(userId);

        if (!await CanUserViewGroupAsync(groupId, userId))
        {
            throw new UnauthorizedAccessException($"User {userId} does not have permission to view group {groupId} members");
        }

        return await context.SharedAutomatonGroupMembers
            .Where(m => m.GroupId == groupId)
            .OrderByDescending(m => m.Role)
            .ThenBy(m => m.JoinedAt)
            .ToListAsync();
    }

    public async Task RemoveMemberAsync(int groupId, string userId, string memberUserId)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(memberUserId);

        if (!await CanUserManageMembersAsync(groupId, userId))
        {
            throw new UnauthorizedAccessException($"User {userId} does not have permission to remove members from group {groupId}");
        }

        var member = await context.SharedAutomatonGroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == memberUserId) ?? throw new InvalidOperationException($"User {memberUserId} is not a member of group {groupId}");

        if (member.Role == SharedGroupRole.Owner)
        {
            throw new InvalidOperationException("Cannot remove the group owner");
        }

        context.SharedAutomatonGroupMembers.Remove(member);
        await context.SaveChangesAsync();

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("User {UserId} removed member {MemberUserId} from group {GroupId}", userId, memberUserId, groupId);
        }
    }

    public async Task UpdateMemberRoleAsync(int groupId, string userId, string memberUserId, SharedGroupRole newRole)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(memberUserId);

        if (!await CanUserManageMembersAsync(groupId, userId))
        {
            throw new UnauthorizedAccessException($"User {userId} does not have permission to update member roles in group {groupId}");
        }

        var member = await context.SharedAutomatonGroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == memberUserId) ?? throw new InvalidOperationException($"User {memberUserId} is not a member of group {groupId}");

        if (member.Role == SharedGroupRole.Owner || newRole == SharedGroupRole.Owner)
        {
            throw new InvalidOperationException("Cannot change owner role");
        }

        member.Role = newRole;
        await context.SaveChangesAsync();

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("User {UserId} updated role of {MemberUserId} to {NewRole} in group {GroupId}",
            userId, memberUserId, newRole, groupId);
        }
    }

    public async Task<bool> CanUserViewGroupAsync(int groupId, string userId)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await context.SharedAutomatonGroupMembers
            .AnyAsync(m => m.GroupId == groupId && m.UserId == userId);
    }

    public async Task<bool> CanUserAddToGroupAsync(int groupId, string userId)
    {
        var role = await GetUserRoleInGroupAsync(groupId, userId);
        return role >= SharedGroupRole.Contributor;
    }

    public async Task<bool> CanUserEditInGroupAsync(int groupId, string userId)
    {
        var role = await GetUserRoleInGroupAsync(groupId, userId);
        return role >= SharedGroupRole.Editor;
    }

    public async Task<bool> CanUserManageMembersAsync(int groupId, string userId)
    {
        var role = await GetUserRoleInGroupAsync(groupId, userId);
        return role >= SharedGroupRole.Admin;
    }

    public async Task<SharedGroupRole?> GetUserRoleInGroupAsync(int groupId, string userId)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var member = await context.SharedAutomatonGroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId);

        return member?.Role;
    }
}
