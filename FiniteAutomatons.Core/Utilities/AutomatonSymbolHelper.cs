namespace FiniteAutomatons.Core.Utilities;

/// <summary>
/// Helper utilities and canonical definitions for automaton related symbols.
/// Centralizes epsilon alias handling to keep controller / services / UI consistent.
/// </summary>
public static class AutomatonSymbolHelper
{
    /// <summary>
    /// Canonical internal representation of epsilon transition in code (\0 already used in models).
    /// </summary>
    public const char EpsilonInternal = '\0';

    /// <summary>
    /// Display representation for epsilon for UI/logging.
    /// </summary>
    public const string EpsilonDisplay = "?";

    /// <summary>
    /// Accepted textual aliases that users may input to denote epsilon.
    /// </summary>
    private static readonly HashSet<string> _epsilonAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        string.Empty, // empty
        EpsilonDisplay, // Greek epsilon
        "?",          // legacy placeholder / earlier UI
        "epsilon",
        "eps",
        "e",
        "lambda",
        "?"
    };

    /// <summary>
    /// Returns true if the supplied symbol text is considered epsilon.
    /// </summary>
    public static bool IsEpsilon(string? symbol) => symbol is null || _epsilonAliases.Contains(symbol.Trim());

    /// <summary>
    /// Enumerates the accepted epsilon aliases (defensive copy).
    /// </summary>
    public static IReadOnlyCollection<string> EpsilonAliases => _epsilonAliases.ToArray();
}
