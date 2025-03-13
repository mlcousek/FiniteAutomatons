using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;

namespace FiniteAutomatons.Core.Interfaces.Converters;

public interface INfaToDfaConverter
{
    DFA Convert(NFA nfa);
}
