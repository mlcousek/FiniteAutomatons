using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;

namespace FiniteAutomatons.Services.Interfaces;

public interface IAutomatonGeneratorService
{
    AutomatonViewModel GenerateRandomAutomaton(
        AutomatonType type,
        int stateCount,
        int transitionCount,
        int alphabetSize = 3,
        double acceptingStateRatio = 0.3,
        int? seed = null);

    AutomatonViewModel GenerateRealisticAutomaton(
        AutomatonType type,
        int stateCount,
        int? seed = null);

    bool ValidateGenerationParameters(AutomatonType type, int stateCount, int transitionCount, int alphabetSize);
}
