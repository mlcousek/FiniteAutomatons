namespace FiniteAutomatons.Core.Utilities;

public static class AutomatonSymbolHelper
{
    /// <summary>
    /// Canonical internal representation of epsilon transition in code (null char used internally).
    /// </summary>
    public const char EpsilonInternal = '\0';

    /// <summary>
    /// Primary display representation for epsilon (Greek small letter epsilon).
    /// </summary>
    public const string EpsilonDisplay = "?"; // U+03B5

    /// <summary>
    /// Alternate display representation (Greek lunate epsilon symbol).
    /// </summary>
    private const string EpsilonDisplayAlt = "?"; // U+03F5

    /// <summary>
    /// Accepted textual aliases that users may input to denote epsilon.
    /// (Removed legacy '?' and plain 'e' to avoid ambiguity.)
    /// </summary>
    private static readonly HashSet<string> _epsilonAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        EpsilonDisplay,      // ?
        EpsilonDisplayAlt,   // ?
        "epsilon",
        "eps",
        "lambda",
        "\\0",            // escaped null
        "\0"               // literal two-char sequence backslash + 0 (from form submissions)
    };

    /// <summary>
    /// Returns true if the supplied symbol text is considered epsilon.
    /// </summary>
    public static bool IsEpsilon(string? symbol)
    {
        if (symbol is null) return false; // no implicit epsilon for null text now
        var trimmed = symbol.Trim();
        if (trimmed.Length == 1)
        {
            var c = trimmed[0];
            if (c == EpsilonInternal || c == EpsilonDisplay[0] || c == EpsilonDisplayAlt[0]) return true;
        }
        return _epsilonAliases.Contains(trimmed);
    }

    /// <summary>
    /// Returns true if the supplied character is considered epsilon.
    /// </summary>
    public static bool IsEpsilonChar(char c)
        => c == EpsilonInternal || c == EpsilonDisplay[0] || c == EpsilonDisplayAlt[0];

    /// <summary>
    /// Enumerates the accepted epsilon aliases (defensive copy).
    /// </summary>
    public static IReadOnlyCollection<string> EpsilonAliases => _epsilonAliases.ToArray();
}
