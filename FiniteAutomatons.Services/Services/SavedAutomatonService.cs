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
        if (saveExecutionState)
        {
            // capture relevant execution state fields
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

        var entity = new SavedAutomaton
        {
            UserId = userId,
            Name = name,
            Description = description,
            ContentJson = payload,
            HasExecutionState = saveExecutionState,
            ExecutionStateJson = execJson,
            CreatedAt = DateTime.UtcNow,
            GroupId = groupId
        };

        db.SavedAutomatons.Add(entity);
        await db.SaveChangesAsync();
        logger.LogInformation("Saved automaton {Id} for user {User} (withState={HasState})", entity.Id, userId, saveExecutionState);
        return entity;
    }

    public async Task<List<SavedAutomaton>> ListForUserAsync(string userId, int? groupId = null)
    {
        var q = db.SavedAutomatons.AsQueryable().Where(s => s.UserId == userId);
        if (groupId.HasValue) q = q.Where(s => s.GroupId == groupId.Value);
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
        var group = new SavedAutomatonGroup { UserId = userId, Name = name, Description = description };
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

    private sealed class AutomatonPayloadDto
    {
        public AutomatonType Type { get; set; }
        public List<FiniteAutomatons.Core.Models.DoMain.State>? States { get; set; }
        public List<FiniteAutomatons.Core.Models.DoMain.Transition>? Transitions { get; set; }
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
