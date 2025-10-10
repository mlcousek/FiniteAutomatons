using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;

namespace FiniteAutomatons.Services.Interfaces;

public interface IRegexToAutomatonService
{
    /// <summary>
    /// Build an epsilon-NFA from a regular expression supporting concatenation, alternation '|', Kleene star '*', plus '+', optional '?', and grouping '()'.
    /// </summary>
    /// <param name="regex">Input regular expression (simple syntax)</param>
    /// <returns>EpsilonNFA that recognizes the same language (best effort for supported constructs)</returns>
    EpsilonNFA BuildEpsilonNfaFromRegex(string regex);
}
