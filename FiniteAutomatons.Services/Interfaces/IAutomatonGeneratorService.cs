using FiniteAutomatons.Core.Models.ViewModel;

namespace FiniteAutomatons.Services.Interfaces;

public interface IAutomatonGeneratorService
{
    AutomatonViewModel GenerateRandomAutomaton(
        AutomatonType type,
        int stateCount,
        int transitionCount,
        int alphabetSize = 4,
        double acceptingStateRatio = 0.3,
        int? seed = null);

    bool ValidateGenerationParameters(AutomatonType type, int stateCount, int transitionCount, int alphabetSize);
}
