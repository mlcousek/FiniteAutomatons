using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
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
        int? seed = null,
        PDAAcceptanceMode? acceptanceMode = null,
        Stack<char>? initialStack = null);

    bool ValidateGenerationParameters(AutomatonType type, int stateCount, int transitionCount, int alphabetSize);

    (int stateCount, int transitionCount, int alphabetSize, double acceptingRatio) GenerateRandomParameters(int? seed = null);
}
