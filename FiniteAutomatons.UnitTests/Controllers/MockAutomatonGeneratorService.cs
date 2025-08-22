using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;

namespace FiniteAutomatons.UnitTests.Controllers;

/// <summary>
/// Mock implementation of IAutomatonGeneratorService for testing purposes
/// </summary>
public class MockAutomatonGeneratorService : IAutomatonGeneratorService
{
    public AutomatonViewModel GenerateRandomAutomaton(AutomatonType type, int stateCount, int transitionCount, int alphabetSize = 3, double acceptingStateRatio = 0.3, int? seed = null)
    {
        return new AutomatonViewModel
        {
            Type = type,
            States = [new State { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            IsCustomAutomaton = true
        };
    }

    public AutomatonViewModel GenerateRealisticAutomaton(AutomatonType type, int stateCount, int? seed = null)
    {
        return GenerateRandomAutomaton(type, stateCount, stateCount, 3, 0.3, seed);
    }

    public bool ValidateGenerationParameters(AutomatonType type, int stateCount, int transitionCount, int alphabetSize)
    {
        return stateCount > 0 && transitionCount >= 0 && alphabetSize > 0;
    }
}