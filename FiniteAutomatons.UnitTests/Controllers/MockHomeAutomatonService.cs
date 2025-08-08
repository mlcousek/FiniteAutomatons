using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;

namespace FiniteAutomatons.UnitTests.Controllers;

public class MockHomeAutomatonService : IHomeAutomatonService
{
    public AutomatonViewModel GenerateDefaultAutomaton()
    {
        // Return a predictable test automaton
        return new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = 
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions = 
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'a' }
            ],
            Alphabet = ['a', 'b', 'c', 'd'],
            IsCustomAutomaton = false
        };
    }

    public AutomatonViewModel CreateFallbackAutomaton()
    {
        return new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = 
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions = 
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }
            ],
            Alphabet = ['a'],
            IsCustomAutomaton = false
        };
    }
}