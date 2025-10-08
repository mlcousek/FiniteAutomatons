using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;

namespace FiniteAutomatons.UnitTests.Controllers;

public class MockAutomatonValidationService : IAutomatonValidationService
{
    public (bool IsValid, List<string> Errors) ValidateAutomaton(AutomatonViewModel model)
    {
        if (model.States.Count > 0 && model.States.Count(s => s.IsStart) == 1)
        {
            return (true, []);
        }
        
        return (false, ["Mock validation failed"]);
    }

    public (bool IsValid, string? ErrorMessage) ValidateStateAddition(AutomatonViewModel model, int stateId, bool isStart)
    {
        if (model.States.Any(s => s.Id == stateId))
        {
            return (false, $"State with ID {stateId} already exists.");
        }
        
        return (true, null);
    }

    public (bool IsValid, char ProcessedSymbol, string? ErrorMessage) ValidateTransitionAddition(AutomatonViewModel model, int fromStateId, int toStateId, string symbol)
    {
        if (string.IsNullOrEmpty(symbol) || symbol == "?")
        {
            return (true, '\0', null); 
        }
        
        if (symbol.Length == 1)
        {
            return (true, symbol[0], null);
        }
        
        return (false, '\0', "Invalid symbol");
    }
}
