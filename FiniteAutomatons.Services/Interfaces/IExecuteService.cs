using FiniteAutomatons.Core.Interfaces;

namespace FiniteAutomatons.Services.Interfaces;

public interface IExecuteService
{
    bool ExecuteAutomaton(string input, IAutomaton automaton);
}
