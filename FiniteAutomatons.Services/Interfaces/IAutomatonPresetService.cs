using FiniteAutomatons.Core.Models.ViewModel;

namespace FiniteAutomatons.Services.Interfaces;

public interface IAutomatonPresetService
{
    AutomatonViewModel GenerateRandomDfa(int stateCount = 5, int transitionCount = 10, int alphabetSize = 3, double acceptingRatio = 0.3, int? seed = null);
    AutomatonViewModel GenerateMinimalizedDfa(int stateCount = 5, int transitionCount = 10, int alphabetSize = 3, double acceptingRatio = 0.3, int? seed = null);
    AutomatonViewModel GenerateUnminimalizedDfa(int stateCount = 5, int transitionCount = 10, int alphabetSize = 3, double acceptingRatio = 0.3, int? seed = null);
    AutomatonViewModel GenerateNondeterministicNfa(int stateCount = 5, int transitionCount = 10, int alphabetSize = 3, double acceptingRatio = 0.3, int? seed = null);
    AutomatonViewModel GenerateRandomNfa(int stateCount = 5, int transitionCount = 10, int alphabetSize = 3, double acceptingRatio = 0.3, int? seed = null);
    AutomatonViewModel GenerateEpsilonNfa(int stateCount = 5, int transitionCount = 10, int alphabetSize = 3, double acceptingRatio = 0.3, int? seed = null);
    AutomatonViewModel GenerateEpsilonNfaNondeterministic(int stateCount = 5, int transitionCount = 10, int alphabetSize = 3, double acceptingRatio = 0.3, int? seed = null);
    AutomatonViewModel GenerateRandomPda(int stateCount = 5, int transitionCount = 10, int alphabetSize = 3, double acceptingRatio = 0.3, int? seed = null);
    AutomatonViewModel GeneratePdaWithPushPopPairs(int stateCount = 5, int transitionCount = 12, int alphabetSize = 3, double acceptingRatio = 0.3, int? seed = null);
    AutomatonViewModel GenerateBalancedParenthesesPda(int maxDepth = 5);
}
