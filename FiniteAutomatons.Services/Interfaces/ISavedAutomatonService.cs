using FiniteAutomatons.Core.Models.Database;
using FiniteAutomatons.Core.Models.ViewModel;

namespace FiniteAutomatons.Services.Interfaces;

public interface ISavedAutomatonService
{
    Task<SavedAutomaton> SaveAsync(string userId, string name, string? description, AutomatonViewModel model, bool saveExecutionState = false, int? groupId = null, string? layoutJson = null, string? thumbnailBase64 = null);
    Task<List<SavedAutomaton>> ListForUserAsync(string userId, int? groupId = null);
    Task<SavedAutomaton?> GetAsync(int id, string userId);
    Task DeleteAsync(int id, string userId);
    Task<SavedAutomatonGroup> CreateGroupAsync(string userId, string name, string? description);
    Task<List<SavedAutomatonGroup>> ListGroupsForUserAsync(string userId);
    Task AddGroupMemberAsync(int groupId, string userId);
    Task RemoveGroupMemberAsync(int groupId, string userId);
    Task<List<SavedAutomatonGroupMember>> ListGroupMembersAsync(int groupId);
    Task<bool> CanUserSaveToGroupAsync(int groupId, string userId);
    Task<SavedAutomatonGroup?> GetGroupAsync(int groupId);
    Task SetGroupSharingPolicyAsync(int groupId, bool membersCanShare);
    Task AssignAutomatonToGroupAsync(int automatonId, string userId, int? groupId);
    Task RemoveAutomatonFromGroupAsync(int automatonId, string userId, int groupId);
    Task DeleteGroupAsync(int groupId, string userId);
}
