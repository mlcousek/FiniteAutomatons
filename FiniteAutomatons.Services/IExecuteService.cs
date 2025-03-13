using FiniteAutomatons.Core.Interfaces;

namespace FiniteAutomatons.Services;

public interface IExecuteService
{
    bool ExecuteAutomaton(string input, IAutomaton automaton);
}
