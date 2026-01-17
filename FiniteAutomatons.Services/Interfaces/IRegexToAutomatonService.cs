using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;

namespace FiniteAutomatons.Services.Interfaces;

public interface IRegexToAutomatonService
{
    EpsilonNFA BuildEpsilonNfaFromRegex(string regex);
}
