using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Core.Utilities;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace FiniteAutomatons.Services.Services;

public class AutomatonEditingService(IAutomatonValidationService validationService, ILogger<AutomatonEditingService> logger) : IAutomatonEditingService
{
    private readonly IAutomatonValidationService validation = validationService;
    private readonly ILogger<AutomatonEditingService> logger = logger;

    public (bool Ok, string? Error) AddState(AutomatonViewModel model, int id, bool isStart, bool isAccepting)
    {
        model.EnsureInitialized();
        var (ok, error) = validation.ValidateStateAddition(model, id, isStart);
        if (!ok) return (false, error);
        model.States.Add(new State { Id = id, IsStart = isStart, IsAccepting = isAccepting });
        model.ClearExecutionState(keepInput: true);
        logger.LogInformation("Added state {Id} (start={Start} accept={Accept})", id, isStart, isAccepting);
        return (true, null);
    }

    public (bool Ok, string? Error) RemoveState(AutomatonViewModel model, int id)
    {
        model.EnsureInitialized();
        var removedStart = model.States.FirstOrDefault(s => s.Id == id)?.IsStart == true;
        int removed = model.States.RemoveAll(s => s.Id == id);
        if (removed == 0) return (false, "State not found");
        model.Transitions.RemoveAll(t => t.FromStateId == id || t.ToStateId == id);
        if (removedStart && model.States.Count > 0)
        {
            model.States[0].IsStart = true;
        }
        model.ClearExecutionState();
        logger.LogInformation("Removed state {Id}", id);
        return (true, null);
    }

    public (bool Ok, char ProcessedSymbol, string? Error) AddTransition(AutomatonViewModel model, int fromId, int toId, string symbol)
    {
        model.EnsureInitialized();
        var (ok, processed, error) = validation.ValidateTransitionAddition(model, fromId, toId, symbol ?? string.Empty);
        if (!ok) return (false, '\0', error);
        model.Transitions.Add(new Transition { FromStateId = fromId, ToStateId = toId, Symbol = processed });
        model.ClearExecutionState(keepInput: true);
        logger.LogInformation("Added transition {From}->{To} '{Sym}'", fromId, toId, processed == AutomatonSymbolHelper.EpsilonInternal ? AutomatonSymbolHelper.EpsilonDisplay : processed);
        return (true, processed, null);
    }

    public (bool Ok, string? Error) RemoveTransition(AutomatonViewModel model, int fromId, int toId, string symbol)
    {
        model.EnsureInitialized();
        char symbolChar;
        if (AutomatonSymbolHelper.IsEpsilon(symbol)) symbolChar = AutomatonSymbolHelper.EpsilonInternal;
        else if (!string.IsNullOrWhiteSpace(symbol) && symbol.Trim().Length == 1) symbolChar = symbol.Trim()[0];
        else return (false, "Invalid symbol format.");

        int removed = model.Transitions.RemoveAll(t => t.FromStateId == fromId && t.ToStateId == toId && t.Symbol == symbolChar);
        if (removed == 0) return (false, "No matching transition found to remove.");
        model.ClearExecutionState();
        logger.LogInformation("Removed transition {From}->{To} '{Sym}'", fromId, toId, symbolChar == AutomatonSymbolHelper.EpsilonInternal ? AutomatonSymbolHelper.EpsilonDisplay : symbolChar);
        return (true, null);
    }
}
