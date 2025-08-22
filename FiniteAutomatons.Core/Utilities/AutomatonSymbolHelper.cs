namespace FiniteAutomatons.Core.Utilities;

public static class AutomatonSymbolHelper
{
    /// <summary>
    /// Canonical internal representation of epsilon transition in code (null char used internally).
    /// </summary>
    public const char EpsilonInternal = '\0';

    /// <summary>
    /// Display representation for epsilon for UI/logging (Greek lowercase epsilon).
    /// </summary>
    public const string EpsilonDisplay = "?"; // primary display

    /// <summary>
    /// Display representation for epsilon (Greek lunate epsilon).
    /// </summary>
    private const string EpsilonDisplayAlt = "?";

    /// <summary>
    /// Accepted textual aliases that users may input to denote epsilon.
    /// Includes legacy "?" for backward compatibility.
    /// </summary>
    private static readonly HashSet<string> _epsilonAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        string.Empty,
        EpsilonDisplay,     // ?
        EpsilonDisplayAlt,  // ?
        "?",               // legacy placeholder
        "epsilon",
        "eps",
        "e",
        "lambda"
    };

    /// <summary>
    /// Returns true if the supplied symbol text is considered epsilon.
    /// </summary>
    public static bool IsEpsilon(string? symbol)
        => symbol is null || _epsilonAliases.Contains(symbol.Trim());

    /// <summary>
    /// Returns true if the supplied character is considered epsilon.
    /// </summary>
    public static bool IsEpsilonChar(char c)
        => c == EpsilonInternal || c == '?' || c == '?' || c == '?' ;

    /// <summary>
    /// Enumerates the accepted epsilon aliases (defensive copy).
    /// </summary>
    public static IReadOnlyCollection<string> EpsilonAliases => _epsilonAliases.ToArray();
}
