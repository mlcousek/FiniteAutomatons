using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;

namespace FiniteAutomatons.UnitTests.Controllers;

public class MockAutomatonGeneratorService : IAutomatonGeneratorService
{
    public AutomatonViewModel GenerateRandomAutomaton(AutomatonType type, int stateCount, int transitionCount, int alphabetSize = 3, double acceptingStateRatio = 0.3, int? seed = null)
    {
        stateCount = Math.Max(1, stateCount);
        var states = new List<State>();
        for (int i = 1; i <= stateCount; i++)
        {
            states.Add(new State { Id = i, IsStart = i == 1, IsAccepting = i == stateCount });
        }

        var transitions = new List<Transition>();
        int targetIdx = stateCount > 1 ? 2 : 1;
        for (int i = 0; i < alphabetSize; i++)
        {
            char symbol = (char)('a' + i);
            transitions.Add(new Transition { FromStateId = 1, ToStateId = targetIdx, Symbol = symbol });
        }

        for (int i = transitions.Count; i < transitionCount; i++)
        {
            transitions.Add(new Transition { FromStateId = 1, ToStateId = 1, Symbol = transitions[0].Symbol });
        }

        return new AutomatonViewModel
        {
            Type = type,
            States = states,
            Transitions = transitions,
            IsCustomAutomaton = true
        };
    }

    public AutomatonViewModel GenerateRealisticAutomaton(
        AutomatonType type,
        int stateCount,
        int? seed = null,
        int minAlphabetSize = 3,
        int maxAlphabetSize = 6,
        double minAcceptingRatio = 0.2,
        double maxAcceptingRatio = 0.5)
    {
        return GenerateRandomAutomaton(type, stateCount, transitionCount: Math.Max(stateCount, 3), alphabetSize: 3, acceptingStateRatio: 0.4, seed: seed);
    }

    public bool ValidateGenerationParameters(AutomatonType type, int stateCount, int transitionCount, int alphabetSize)
    {
        return stateCount > 0 && transitionCount >= 0 && alphabetSize > 0;
    }
}
