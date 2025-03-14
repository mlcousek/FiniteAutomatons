using FiniteAutomatons.Core.Interfaces;

namespace FiniteAutomatons.Services.AutomatonsSharedServices;

public interface IExecuteService
{
    bool ExecuteAutomaton(string input, IAutomaton automaton);
}
