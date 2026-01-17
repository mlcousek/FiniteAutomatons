using FiniteAutomatons.Core.Models.ViewModel;

namespace FiniteAutomatons.Services.Interfaces;

public interface IAutomatonValidationService
{
    (bool IsValid, List<string> Errors) ValidateAutomaton(AutomatonViewModel model);
    (bool IsValid, string? ErrorMessage) ValidateStateAddition(AutomatonViewModel model, int stateId, bool isStart);
    (bool IsValid, char ProcessedSymbol, string? ErrorMessage) ValidateTransitionAddition(AutomatonViewModel model, int fromStateId, int toStateId, string symbol);
}
