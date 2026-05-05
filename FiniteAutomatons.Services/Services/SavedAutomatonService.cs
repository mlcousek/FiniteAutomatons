using FiniteAutomatons.Core.Models.Database;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Data;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FiniteAutomatons.Services.Services;

public class SavedAutomatonService(ILogger<SavedAutomatonService> logger, ApplicationDbContext db) : ISavedAutomatonService
{
    private readonly ILogger<SavedAutomatonService> logger = logger;
    private readonly ApplicationDbContext db = db;

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public async Task<SavedAutomaton> SaveAsync(string userId, string name, string? description, AutomatonViewModel model, bool saveExecutionState = false, int? groupId = null, string? layoutJson = null, string? thumbnailBase64 = null)
    {
        ValidateSaveParameters(userId, name, model);

        model.States ??= [];
        model.Transitions ??= [];

        if (groupId.HasValue)
        {
            await ValidateGroupPermissionsAsync(groupId.Value, userId);
        }

        var payload = SerializeAutomatonPayload(model);
        var (saveMode, execJson) = DetermineSaveModeAndExecutionState(model, saveExecutionState);
        var entity = CreateSavedAutomatonEntity(userId, name, description, model, payload, saveMode, execJson, layoutJson, thumbnailBase64);

        db.SavedAutomatons.Add(entity);
        await db.SaveChangesAsync();

        if (groupId.HasValue)
        {
            await AssignToGroupIfNotExistsAsync(entity.Id, groupId.Value);
        }

        LogSaveSuccess(entity.Id, userId, saveMode);
        return entity;
    }

    private static void ValidateSaveParameters(string userId, string name, AutomatonViewModel model)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(model);
    }

    private async Task ValidateGroupPermissionsAsync(int groupId, string userId)
    {
        var grp = await db.SavedAutomatonGroups.FirstOrDefaultAsync(g => g.Id == groupId)
            ?? throw new InvalidOperationException("Group not found");

        if (!grp.MembersCanShare && grp.UserId != userId)
        {
            throw new UnauthorizedAccessException("You are not allowed to save into this group.");
        }

        if (grp.MembersCanShare && grp.UserId != userId)
        {
            await ValidateGroupMembershipAsync(grp.Id, userId);
        }
    }

    private async Task ValidateGroupMembershipAsync(int groupId, string userId)
    {
        var isMember = await db.SavedAutomatonGroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == userId);
        if (!isMember)
        {
            throw new UnauthorizedAccessException("You are not a member of this group.");
        }
    }

    private static string SerializeAutomatonPayload(AutomatonViewModel model)
    {
        return System.Text.Json.JsonSerializer.Serialize(new AutomatonPayloadDto
        {
            Type = model.Type,
            States = model.States,
            Transitions = model.Transitions
        }, JsonOptions);
    }

    private static (AutomatonSaveMode SaveMode, string? ExecJson) DetermineSaveModeAndExecutionState(AutomatonViewModel model, bool saveExecutionState)
    {
        if (saveExecutionState)
        {
            return (AutomatonSaveMode.WithState, SerializeFullExecutionState(model));
        }

        if (!string.IsNullOrEmpty(model.Input))
        {
            return (AutomatonSaveMode.WithInput, SerializeInputOnly(model));
        }

        return (AutomatonSaveMode.Structure, null);
    }

    private static string SerializeFullExecutionState(AutomatonViewModel model)
    {
        var exec = new SavedExecutionStateDto
        {
            Input = model.Input,
            Position = model.Position,
            CurrentStateId = model.CurrentStateId,
            CurrentStates = model.CurrentStates?.ToList(),
            IsAccepted = model.IsAccepted,
            StateHistorySerialized = model.StateHistorySerialized,
            StackSerialized = model.StackSerialized
        };
        return System.Text.Json.JsonSerializer.Serialize(exec, JsonOptions);
    }

    private static string SerializeInputOnly(AutomatonViewModel model)
    {
        var exec = new SavedExecutionStateDto
        {
            Input = model.Input,
            Position = 0,
            CurrentStateId = null,
            CurrentStates = null,
            IsAccepted = null,
            StateHistorySerialized = string.Empty,
            StackSerialized = null
        };
        return System.Text.Json.JsonSerializer.Serialize(exec, JsonOptions);
    }

    private static SavedAutomaton CreateSavedAutomatonEntity(string userId, string name, string? description,
        AutomatonViewModel model, string payload, AutomatonSaveMode saveMode, string? execJson,
        string? layoutJson, string? thumbnailBase64)
    {
        return new SavedAutomaton
        {
            UserId = userId,
            Name = name,
            Description = description,
            ContentJson = payload,
            SaveMode = saveMode,
            ExecutionStateJson = execJson,
            LayoutJson = string.IsNullOrWhiteSpace(layoutJson) ? null : layoutJson,
            ThumbnailBase64 = string.IsNullOrWhiteSpace(thumbnailBase64) ? null : thumbnailBase64,
            SourceRegex = model.SourceRegex,
            CreatedAt = DateTime.UtcNow,
        };
    }

    private async Task AssignToGroupIfNotExistsAsync(int automatonId, int groupId)
    {
        var exists = await db.SavedAutomatonGroupAssignments.AnyAsync(a => a.AutomatonId == automatonId && a.GroupId == groupId);
        if (!exists)
        {
            db.SavedAutomatonGroupAssignments.Add(new SavedAutomatonGroupAssignment
            {
                AutomatonId = automatonId,
                GroupId = groupId
            });
            await db.SaveChangesAsync();
        }
    }

    private void LogSaveSuccess(int automatonId, string userId, AutomatonSaveMode saveMode)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Saved automaton {Id} for user {User} (saveMode={SaveMode})",
                automatonId, userId, saveMode);
        }
    }

    public async Task<SavedAutomaton> UpdateAsync(int id, string userId, string name, string? description, AutomatonViewModel model, bool saveExecutionState = false, string? layoutJson = null, string? thumbnailBase64 = null)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(model);

        var entity = await db.SavedAutomatons.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId)
            ?? throw new InvalidOperationException("Automaton not found or access denied.");

        model.States ??= [];
        model.Transitions ??= [];

        var payload = SerializeAutomatonPayload(model);
        var (saveMode, execJson) = DetermineSaveModeAndExecutionState(model, saveExecutionState);

        entity.Name = name;
        entity.Description = description;
        entity.ContentJson = payload;
        entity.SaveMode = saveMode;
        entity.ExecutionStateJson = execJson;
        entity.LayoutJson = string.IsNullOrWhiteSpace(layoutJson) ? null : layoutJson;
        entity.ThumbnailBase64 = string.IsNullOrWhiteSpace(thumbnailBase64) ? null : thumbnailBase64;
        entity.SourceRegex = model.SourceRegex;

        await db.SaveChangesAsync();

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Updated automaton {Id} for user {User} (saveMode={SaveMode})", id, userId, saveMode);
        }

        return entity;
    }

    public async Task DeleteGroupAsync(int groupId, string userId)
    {
        var grp = await db.SavedAutomatonGroups.FirstOrDefaultAsync(g => g.Id == groupId);
        if (grp == null) return;
        if (grp.UserId != userId) throw new UnauthorizedAccessException("Only owner may delete the group.");

        var assigns = await db.SavedAutomatonGroupAssignments.Where(a => a.GroupId == groupId).ToListAsync();
        if (assigns.Count != 0) db.SavedAutomatonGroupAssignments.RemoveRange(assigns);

        db.SavedAutomatonGroups.Remove(grp);
        await db.SaveChangesAsync();
    }

    public async Task<List<SavedAutomaton>> ListForUserAsync(string userId, int? groupId = null)
    {
        var q = db.SavedAutomatons
            .Include(s => s.Assignments)
            .ThenInclude(a => a.Group)
            .Where(s => s.UserId == userId);

        if (groupId.HasValue)
        {
            q = q.Where(s => s.Assignments.Any(a => a.GroupId == groupId.Value));
        }

        return await q.OrderByDescending(s => s.CreatedAt).ToListAsync();
    }

    public async Task<SavedAutomaton?> GetAsync(int id, string userId)
    {
        return await db.SavedAutomatons.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);
    }

    public async Task DeleteAsync(int id, string userId)
    {
        var entity = await db.SavedAutomatons.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);
        if (entity == null) return;
        db.SavedAutomatons.Remove(entity);
        await db.SaveChangesAsync();
    }

    public async Task<SavedAutomatonGroup> CreateGroupAsync(string userId, string name, string? description)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(name);

        var normalized = name.Trim();
        var userGroups = await db.SavedAutomatonGroups.Where(g => g.UserId == userId).ToListAsync();
        if (userGroups.Any(g => string.Equals(g.Name, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("A group with the same name already exists.");
        }

        var group = new SavedAutomatonGroup { UserId = userId, Name = normalized, Description = description };
        db.SavedAutomatonGroups.Add(group);
        await db.SaveChangesAsync();
        return group;
    }

    public async Task<List<SavedAutomatonGroup>> ListGroupsForUserAsync(string userId)
    {
        return await db.SavedAutomatonGroups.Where(g => g.UserId == userId).OrderBy(g => g.Name).ToListAsync();
    }

    public async Task AddGroupMemberAsync(int groupId, string userId)
    {
        var grp = await db.SavedAutomatonGroups.FindAsync(groupId) ?? throw new InvalidOperationException("Group not found");
        if (grp.UserId == userId) return;
        var exists = await db.SavedAutomatonGroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == userId);
        if (exists) return;
        var mem = new SavedAutomatonGroupMember { GroupId = groupId, UserId = userId };
        db.SavedAutomatonGroupMembers.Add(mem);
        await db.SaveChangesAsync();
    }

    public async Task RemoveGroupMemberAsync(int groupId, string userId)
    {
        var mem = await db.SavedAutomatonGroupMembers.FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId);
        if (mem == null) return;
        db.SavedAutomatonGroupMembers.Remove(mem);
        await db.SaveChangesAsync();
    }

    public async Task<List<SavedAutomatonGroupMember>> ListGroupMembersAsync(int groupId)
    {
        return await db.SavedAutomatonGroupMembers.Where(m => m.GroupId == groupId).ToListAsync();
    }

    public async Task<bool> CanUserSaveToGroupAsync(int groupId, string userId)
    {
        var grp = await db.SavedAutomatonGroups.FindAsync(groupId);
        if (grp == null) return false;
        if (grp.UserId == userId) return true;
        if (!grp.MembersCanShare) return false;
        var isMember = await db.SavedAutomatonGroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == userId);
        return isMember;
    }

    public async Task<SavedAutomatonGroup?> GetGroupAsync(int groupId)
    {
        return await db.SavedAutomatonGroups.FirstOrDefaultAsync(g => g.Id == groupId);
    }

    public async Task SetGroupSharingPolicyAsync(int groupId, bool membersCanShare)
    {
        var g = await db.SavedAutomatonGroups.FindAsync(groupId) ?? throw new InvalidOperationException("Group not found");
        g.MembersCanShare = membersCanShare;
        await db.SaveChangesAsync();
    }

    public async Task AssignAutomatonToGroupAsync(int automatonId, string userId, int? groupId)
    {
        await ValidateAutomatonOwnershipAsync(automatonId, userId);

        if (!groupId.HasValue)
        {
            await UnassignFromAllGroupsAsync(automatonId);
            return;
        }

        await ValidateGroupAndPermissionsAsync(groupId.Value, userId);
        await AssignToGroupIfNotExistsAsync(automatonId, groupId.Value);
    }

    private async Task ValidateAutomatonOwnershipAsync(int automatonId, string userId)
    {
        var entity = await db.SavedAutomatons.FirstOrDefaultAsync(s => s.Id == automatonId && s.UserId == userId) ?? throw new InvalidOperationException("Automaton not found or access denied.");
    }

    private async Task UnassignFromAllGroupsAsync(int automatonId)
    {
        var assigns = await db.SavedAutomatonGroupAssignments
            .Where(a => a.AutomatonId == automatonId)
            .ToListAsync();

        if (assigns.Count != 0)
        {
            db.SavedAutomatonGroupAssignments.RemoveRange(assigns);
            await db.SaveChangesAsync();
        }
    }

    private async Task ValidateGroupAndPermissionsAsync(int groupId, string userId)
    {
        var grp = await db.SavedAutomatonGroups.FirstOrDefaultAsync(g => g.Id == groupId)
            ?? throw new InvalidOperationException("Group not found");

        if (!await CanUserSaveToGroupAsync(grp.Id, userId))
        {
            throw new UnauthorizedAccessException("You are not allowed to assign to this group.");
        }
    }

    public async Task RemoveAutomatonFromGroupAsync(int automatonId, string userId, int groupId)
    {
        var entity = await db.SavedAutomatons.FirstOrDefaultAsync(s => s.Id == automatonId && s.UserId == userId) ?? throw new InvalidOperationException("Automaton not found or access denied.");
        var ass = await db.SavedAutomatonGroupAssignments.FirstOrDefaultAsync(a => a.AutomatonId == automatonId && a.GroupId == groupId);
        if (ass == null) return;
        db.SavedAutomatonGroupAssignments.Remove(ass);
        await db.SaveChangesAsync();
    }

    private sealed class AutomatonPayloadDto
    {
        public AutomatonType Type { get; set; }
        public List<Core.Models.DoMain.State>? States { get; set; }
        public List<Core.Models.DoMain.Transition>? Transitions { get; set; }
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
