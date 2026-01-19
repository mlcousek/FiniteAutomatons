using FiniteAutomatons.Core.Models.ViewModel;

namespace FiniteAutomatons.Services.Interfaces;

public interface IInputGenerationService
{

    string GenerateRandomString(AutomatonViewModel automaton, int minLength = 0, int maxLength = 10, int? seed = null);

    string? GenerateAcceptingString(AutomatonViewModel automaton, int maxLength = 20);

    string? GenerateRandomAcceptingString(AutomatonViewModel automaton, int minLength = 0, int maxLength = 50, int maxAttempts = 100, int? seed = null);

    string? GenerateRejectingString(AutomatonViewModel automaton, int maxLength = 20);

    List<(string Input, string Description)> GenerateInterestingCases(AutomatonViewModel automaton, int maxLength = 15);

    string? GenerateNondeterministicCase(AutomatonViewModel automaton, int maxLength = 15);

    string? GenerateEpsilonCase(AutomatonViewModel automaton, int maxLength = 15);
}
