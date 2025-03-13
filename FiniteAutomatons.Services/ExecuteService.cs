using FiniteAutomatons.Core.Interfaces;

namespace FiniteAutomatons.Services;

public class ExecuteService : IExecuteService
{
    public bool ExecuteAutomaton(string input, IAutomaton automaton)
    {
        return automaton.Execute(input);
    }
}
