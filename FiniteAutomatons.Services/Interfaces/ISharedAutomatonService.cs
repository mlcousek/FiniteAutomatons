using FiniteAutomatons.Core.Models.Database;
using FiniteAutomatons.Core.Models.ViewModel;

namespace FiniteAutomatons.Services.Interfaces;

/// <summary>
/// Service for managing shared automatons in collaborative groups
/// </summary>
public interface ISharedAutomatonService
{
    // Basic CRUD operations
    Task<SharedAutomaton> SaveAsync(string userId, int groupId, string name, string? description, AutomatonViewModel model, bool saveExecutionState = false);
    Task<SharedAutomaton?> GetAsync(int id, string userId);
    Task<List<SharedAutomaton>> ListForGroupAsync(int groupId, string userId);
    Task<List<SharedAutomaton>> ListForUserAsync(string userId);
    Task DeleteAsync(int id, string userId);
    Task<SharedAutomaton> UpdateAsync(int id, string userId, string? name, string? description, AutomatonViewModel? model);
    
    // Group management
    Task<SharedAutomatonGroup> CreateGroupAsync(string userId, string name, string? description);
    Task<SharedAutomatonGroup?> GetGroupAsync(int groupId, string userId);
    Task<List<SharedAutomatonGroup>> ListGroupsForUserAsync(string userId);
    Task DeleteGroupAsync(int groupId, string userId);
    Task UpdateGroupAsync(int groupId, string userId, string? name, string? description);
    
    // Member management
    Task<List<SharedAutomatonGroupMember>> ListGroupMembersAsync(int groupId, string userId);
    Task RemoveMemberAsync(int groupId, string userId, string memberUserId);
    Task UpdateMemberRoleAsync(int groupId, string userId, string memberUserId, SharedGroupRole newRole);
    
    // Permission checking
    Task<bool> CanUserViewGroupAsync(int groupId, string userId);
    Task<bool> CanUserAddToGroupAsync(int groupId, string userId);
    Task<bool> CanUserEditInGroupAsync(int groupId, string userId);
    Task<bool> CanUserManageMembersAsync(int groupId, string userId);
    Task<SharedGroupRole?> GetUserRoleInGroupAsync(int groupId, string userId);
}
