using FiniteAutomatons.Core.Models.ViewModel;

namespace FiniteAutomatons.Services.Interfaces;

public interface IAutomatonGeneratorService
{
    AutomatonViewModel GenerateRandomAutomaton(
        AutomatonType type,
        int stateCount,
        int transitionCount,
        int alphabetSize = 4,   //TODO introduce constants
        double acceptingStateRatio = 0.3,
        int? seed = null);

    AutomatonViewModel GenerateRealisticAutomaton(
        AutomatonType type,
        int stateCount,
        int? seed = null);

    bool ValidateGenerationParameters(AutomatonType type, int stateCount, int transitionCount, int alphabetSize);
}
