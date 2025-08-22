using FiniteAutomatons.Core.Models.ViewModel;

namespace FiniteAutomatons.Services.Interfaces;

public interface IAutomatonEditingService
{
    (bool Ok, string? Error) AddState(AutomatonViewModel model, int id, bool isStart, bool isAccepting);
    (bool Ok, string? Error) RemoveState(AutomatonViewModel model, int id);
    (bool Ok, char ProcessedSymbol, string? Error) AddTransition(AutomatonViewModel model, int fromId, int toId, string symbol);
    (bool Ok, string? Error) RemoveTransition(AutomatonViewModel model, int fromId, int toId, string symbol);
}
