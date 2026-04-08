using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
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
    AutomatonViewModel GenerateRandomPda(int stateCount = 5, int transitionCount = 10, int alphabetSize = 3, double acceptingRatio = 0.3, int? seed = null, PDAAcceptanceMode? acceptanceMode = null, Stack<char>? initialStack = null, AutomatonType pdaType = AutomatonType.DPDA);
    AutomatonViewModel GeneratePdaWithPushPopPairs(int stateCount = 5, int transitionCount = 12, int alphabetSize = 3, double acceptingRatio = 0.3, int? seed = null, PDAAcceptanceMode? acceptanceMode = null, Stack<char>? initialStack = null, AutomatonType pdaType = AutomatonType.DPDA);
    AutomatonViewModel GenerateBalancedParenthesesPda(PDAAcceptanceMode? acceptanceMode = null, Stack<char>? initialStack = null);
    AutomatonViewModel GenerateAnBnPda(PDAAcceptanceMode? acceptanceMode = null, Stack<char>? initialStack = null);
    AutomatonViewModel GenerateEvenPalindromePda(PDAAcceptanceMode? acceptanceMode = null, Stack<char>? initialStack = null);
    AutomatonViewModel GenerateSimpleCfgPda(PDAAcceptanceMode? acceptanceMode = null, Stack<char>? initialStack = null);

    string GetPresetDisplayName(string preset);
}
