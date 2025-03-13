using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;

namespace FiniteAutomatons.Core.Interfaces.Converters;

public interface IEpsilonNfaToNfaConverter
{
    NFA Convert(EpsilonNFA epsilonNfa);
}
