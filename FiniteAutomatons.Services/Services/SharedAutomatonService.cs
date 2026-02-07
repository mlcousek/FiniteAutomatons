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

    #region CRUD Operations

    public async Task<SharedAutomaton> SaveAsync(string userId, int groupId, string name, string? description, AutomatonViewModel model, bool saveExecutionState = false)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(model);

        // Check permissions
        if (!await CanUserAddToGroupAsync(groupId, userId))
        {
            throw new UnauthorizedAccessException($"User {userId} does not have permission to add automatons to group {groupId}");
        }

        var payload = new AutomatonPayloadDto
        {
            Type = model.Type,
            States = model.States,
            Transitions = model.Transitions
        };

        var automaton = new SharedAutomaton
        {
            CreatedByUserId = userId,
            Name = name.Trim(),
            Description = description?.Trim(),
            ContentJson = JsonSerializer.Serialize(payload),
            SaveMode = AutomatonSaveMode.Structure,
            SourceRegex = model.SourceRegex,
            CreatedAt = DateTime.UtcNow
        };

        // Handle execution state
        if (saveExecutionState && !string.IsNullOrWhiteSpace(model.Input))
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

            automaton.SaveMode = AutomatonSaveMode.WithState;
            automaton.ExecutionStateJson = JsonSerializer.Serialize(execState);
        }
        else if (!string.IsNullOrWhiteSpace(model.Input))
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

            automaton.SaveMode = AutomatonSaveMode.WithInput;
            automaton.ExecutionStateJson = JsonSerializer.Serialize(execState);
        }

        context.SharedAutomatons.Add(automaton);
        await context.SaveChangesAsync();

        // Assign to group
        var assignment = new SharedAutomatonGroupAssignment
        {
            AutomatonId = automaton.Id,
            GroupId = groupId,
            AssignedAt = DateTime.UtcNow
        };

        context.SharedAutomatonGroupAssignments.Add(assignment);
        await context.SaveChangesAsync();

        logger.LogInformation("User {UserId} saved shared automaton {AutomatonId} '{Name}' to group {GroupId}",
            userId, automaton.Id, name, groupId);

        return automaton;
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

        // Check if user has access to any group containing this automaton
        var hasAccess = automaton.Assignments.Any(assignment =>
            assignment.Group != null &&
            assignment.Group.Members.Any(m => m.UserId == userId));

        return hasAccess ? automaton : null;
    }

    public async Task<List<SharedAutomaton>> ListForGroupAsync(int groupId, string userId)
    {
        ArgumentNullException.ThrowIfNull(userId);

        // Check permissions
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

        // Get all groups the user is a member of
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

        var automaton = await context.SharedAutomatons
            .Include(a => a.Assignments)
                .ThenInclude(a => a.Group)
                    .ThenInclude(g => g!.Members)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (automaton == null)
        {
            throw new InvalidOperationException($"Automaton {id} not found");
        }

        // Check if user can delete (must be creator or have Editor/Admin/Owner role in at least one group)
        var canDelete = automaton.CreatedByUserId == userId;

        if (!canDelete)
        {
            foreach (var assignment in automaton.Assignments)
            {
                var userRole = await GetUserRoleInGroupAsync(assignment.GroupId, userId);
                if (userRole >= SharedGroupRole.Editor)
                {
                    canDelete = true;
                    break;
                }
            }
        }

        if (!canDelete)
        {
            throw new UnauthorizedAccessException($"User {userId} does not have permission to delete automaton {id}");
        }

        context.SharedAutomatons.Remove(automaton);
        await context.SaveChangesAsync();

        logger.LogInformation("User {UserId} deleted shared automaton {AutomatonId}", userId, id);
    }

    public async Task<SharedAutomaton> UpdateAsync(int id, string userId, string? name, string? description, AutomatonViewModel? model)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var automaton = await context.SharedAutomatons
            .Include(a => a.Assignments)
                .ThenInclude(a => a.Group)
                    .ThenInclude(g => g!.Members)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (automaton == null)
        {
            throw new InvalidOperationException($"Automaton {id} not found");
        }

        // Check if user can edit (must be creator or have Editor/Admin/Owner role)
        var canEdit = automaton.CreatedByUserId == userId;

        if (!canEdit)
        {
            foreach (var assignment in automaton.Assignments)
            {
                var userRole = await GetUserRoleInGroupAsync(assignment.GroupId, userId);
                if (userRole >= SharedGroupRole.Editor)
                {
                    canEdit = true;
                    break;
                }
            }
        }

        if (!canEdit)
        {
            throw new UnauthorizedAccessException($"User {userId} does not have permission to edit automaton {id}");
        }

        // Update fields
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

        await context.SaveChangesAsync();

        logger.LogInformation("User {UserId} updated shared automaton {AutomatonId}", userId, id);

        return automaton;
    }

    #endregion

    #region Group Management

    public async Task<SharedAutomatonGroup> CreateGroupAsync(string userId, string name, string? description)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(name);

        var group = new SharedAutomatonGroup
        {
            UserId = userId,
            Name = name.Trim(),
            Description = description?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        context.SharedAutomatonGroups.Add(group);
        await context.SaveChangesAsync();

        // Add creator as Owner
        var member = new SharedAutomatonGroupMember
        {
            GroupId = group.Id,
            UserId = userId,
            Role = SharedGroupRole.Owner,
            JoinedAt = DateTime.UtcNow
        };

        context.SharedAutomatonGroupMembers.Add(member);
        await context.SaveChangesAsync();

        logger.LogInformation("User {UserId} created shared group {GroupId} '{Name}'", userId, group.Id, name);

        return group;
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

        // Check if user is a member
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
            .FirstOrDefaultAsync(g => g.Id == groupId);

        if (group == null)
        {
            throw new InvalidOperationException($"Group {groupId} not found");
        }

        // Only owner can delete
        if (group.UserId != userId)
        {
            throw new UnauthorizedAccessException($"User {userId} is not the owner of group {groupId}");
        }

        context.SharedAutomatonGroups.Remove(group);
        await context.SaveChangesAsync();

        logger.LogInformation("User {UserId} deleted shared group {GroupId}", userId, groupId);
    }

    public async Task UpdateGroupAsync(int groupId, string userId, string? name, string? description)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var group = await context.SharedAutomatonGroups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == groupId);

        if (group == null)
        {
            throw new InvalidOperationException($"Group {groupId} not found");
        }

        // Check if user is Admin or Owner
        if (!await CanUserManageMembersAsync(groupId, userId))
        {
            throw new UnauthorizedAccessException($"User {userId} does not have permission to update group {groupId}");
        }

        if (!string.IsNullOrWhiteSpace(name))
            group.Name = name.Trim();

        if (description != null)
            group.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        await context.SaveChangesAsync();

        logger.LogInformation("User {UserId} updated shared group {GroupId}", userId, groupId);
    }

    #endregion

    #region Member Management

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
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == memberUserId);

        if (member == null)
        {
            throw new InvalidOperationException($"User {memberUserId} is not a member of group {groupId}");
        }

        // Cannot remove the owner
        if (member.Role == SharedGroupRole.Owner)
        {
            throw new InvalidOperationException("Cannot remove the group owner");
        }

        context.SharedAutomatonGroupMembers.Remove(member);
        await context.SaveChangesAsync();

        logger.LogInformation("User {UserId} removed member {MemberUserId} from group {GroupId}", userId, memberUserId, groupId);
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
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == memberUserId);

        if (member == null)
        {
            throw new InvalidOperationException($"User {memberUserId} is not a member of group {groupId}");
        }

        // Cannot change owner role
        if (member.Role == SharedGroupRole.Owner || newRole == SharedGroupRole.Owner)
        {
            throw new InvalidOperationException("Cannot change owner role");
        }

        member.Role = newRole;
        await context.SaveChangesAsync();

        logger.LogInformation("User {UserId} updated role of {MemberUserId} to {NewRole} in group {GroupId}",
            userId, memberUserId, newRole, groupId);
    }

    #endregion

    #region Permission Checking

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

    #endregion
}
