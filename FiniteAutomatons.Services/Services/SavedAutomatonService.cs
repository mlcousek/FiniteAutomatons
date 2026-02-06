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

    public async Task<SavedAutomaton> SaveAsync(string userId, string name, string? description, AutomatonViewModel model, bool saveExecutionState = false, int? groupId = null)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(model);

        model.States ??= new List<FiniteAutomatons.Core.Models.DoMain.State>();
        model.Transitions ??= new List<FiniteAutomatons.Core.Models.DoMain.Transition>();

        // if saving into a group, ensure permission
        if (groupId.HasValue)
        {
            var grp = await db.SavedAutomatonGroups.FirstOrDefaultAsync(g => g.Id == groupId.Value);
            if (grp == null) throw new InvalidOperationException("Group not found");
            if (!grp.MembersCanShare && grp.UserId != userId)
            {
                // only owner may save
                throw new UnauthorizedAccessException("You are not allowed to save into this group.");
            }
            else
            {
                // if group allows members to share, ensure the user is member or owner
                if (grp.MembersCanShare && grp.UserId != userId)
                {
                    var isMember = await db.SavedAutomatonGroupMembers.AnyAsync(m => m.GroupId == grp.Id && m.UserId == userId);
                    if (!isMember) throw new UnauthorizedAccessException("You are not a member of this group.");
                }
            }
        }

        // Serialize a payload that can be deserialized back into AutomatonViewModel
        var payload = System.Text.Json.JsonSerializer.Serialize(new AutomatonPayloadDto
        {
            Type = model.Type,
            States = model.States,
            Transitions = model.Transitions
        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

        string? execJson = null;
        AutomatonSaveMode saveMode = AutomatonSaveMode.Structure;

        if (saveExecutionState)
        {
            // Full execution state saved
            saveMode = AutomatonSaveMode.WithState;
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
            execJson = System.Text.Json.JsonSerializer.Serialize(exec, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        else if (!string.IsNullOrEmpty(model.Input))
        {
            // Only input saved (no execution state)
            saveMode = AutomatonSaveMode.WithInput;
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
            execJson = System.Text.Json.JsonSerializer.Serialize(exec, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }

        var entity = new SavedAutomaton
        {
            UserId = userId,
            Name = name,
            Description = description,
            ContentJson = payload,
            SaveMode = saveMode,
            ExecutionStateJson = execJson,
            CreatedAt = DateTime.UtcNow,
        };

        db.SavedAutomatons.Add(entity);
        await db.SaveChangesAsync();
        // if saving into a specific group, create an assignment (many-to-many)
        if (groupId.HasValue)
        {
            var exists = await db.SavedAutomatonGroupAssignments.AnyAsync(a => a.AutomatonId == entity.Id && a.GroupId == groupId.Value);
            if (!exists)
            {
                db.SavedAutomatonGroupAssignments.Add(new SavedAutomatonGroupAssignment { AutomatonId = entity.Id, GroupId = groupId.Value });
                await db.SaveChangesAsync();
            }
        }
        logger.LogInformation("Saved automaton {Id} for user {User} (saveMode={SaveMode})", entity.Id, userId, saveMode);
        return entity;
    }

    public async Task DeleteGroupAsync(int groupId, string userId)
    {
        var grp = await db.SavedAutomatonGroups.FirstOrDefaultAsync(g => g.Id == groupId);
        if (grp == null) return;
        if (grp.UserId != userId) throw new UnauthorizedAccessException("Only owner may delete the group.");

        // remove assignments for this group (many-to-many)
        var assigns = await db.SavedAutomatonGroupAssignments.Where(a => a.GroupId == groupId).ToListAsync();
        if (assigns.Any()) db.SavedAutomatonGroupAssignments.RemoveRange(assigns);

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
        // load existing groups for the user and check duplicates in-memory (avoids translation issues)
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
        if (grp.UserId == userId) return; // owner is implicitly member
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
        var entity = await db.SavedAutomatons.FirstOrDefaultAsync(s => s.Id == automatonId && s.UserId == userId);
        if (entity == null) throw new InvalidOperationException("Automaton not found or access denied.");

        if (!groupId.HasValue)
        {
            var assigns = await db.SavedAutomatonGroupAssignments.Where(a => a.AutomatonId == automatonId).ToListAsync();
            if (assigns.Any())
            {
                db.SavedAutomatonGroupAssignments.RemoveRange(assigns);
                await db.SaveChangesAsync();
            }
            return;
        }

        var grp = await db.SavedAutomatonGroups.FirstOrDefaultAsync(g => g.Id == groupId.Value);
        if (grp == null) throw new InvalidOperationException("Group not found");
        if (!await CanUserSaveToGroupAsync(grp.Id, userId)) throw new UnauthorizedAccessException("You are not allowed to assign to this group.");

        var exists = await db.SavedAutomatonGroupAssignments.AnyAsync(a => a.AutomatonId == automatonId && a.GroupId == groupId.Value);
        if (exists) return;
        db.SavedAutomatonGroupAssignments.Add(new SavedAutomatonGroupAssignment { AutomatonId = automatonId, GroupId = groupId.Value });
        await db.SaveChangesAsync();
    }

    public async Task RemoveAutomatonFromGroupAsync(int automatonId, string userId, int groupId)
    {
        var entity = await db.SavedAutomatons.FirstOrDefaultAsync(s => s.Id == automatonId && s.UserId == userId);
        if (entity == null) throw new InvalidOperationException("Automaton not found or access denied.");
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
