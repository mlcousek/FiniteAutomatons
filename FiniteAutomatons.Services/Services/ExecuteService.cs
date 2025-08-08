using FiniteAutomatons.Core.Interfaces;
using FiniteAutomatons.Services.Interfaces;

namespace FiniteAutomatons.Services.Services;

public class ExecuteService : IExecuteService
{
    public bool ExecuteAutomaton(string input, IAutomaton automaton)
    {
        return automaton.Execute(input);
    }
}
