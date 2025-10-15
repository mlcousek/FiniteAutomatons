using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Core.Models.Database;

namespace FiniteAutomatons.Services.Interfaces;

public interface ISavedAutomatonService
{
    Task<SavedAutomaton> SaveAsync(string userId, string name, string? description, AutomatonViewModel model, bool saveExecutionState = false, int? groupId = null);
    Task<List<SavedAutomaton>> ListForUserAsync(string userId, int? groupId = null);
    Task<SavedAutomaton?> GetAsync(int id, string userId);
    Task DeleteAsync(int id, string userId);
    Task<SavedAutomatonGroup> CreateGroupAsync(string userId, string name, string? description);
    Task<List<SavedAutomatonGroup>> ListGroupsForUserAsync(string userId);
}
