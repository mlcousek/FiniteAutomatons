namespace FiniteAutomatons.Core.Utilities;

public static class AutomatonSymbolHelper
{

    public const char EpsilonInternal = '\0';

    public const string EpsilonDisplay = "ε"; // U+03B5

    private const string EpsilonDisplayAlt = "?"; // U+03F5 //TODO repair

    private static readonly HashSet<string> epsilonAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        EpsilonDisplay,
        EpsilonDisplayAlt,
        "?",
        "epsilon",
        "eps",
        "lambda",
        "\\0",
        "\0"
    };

    public static bool IsEpsilon(string? symbol)
    {
        if (symbol is null) return false;
        var trimmed = symbol.Trim();
        if (trimmed.Length == 1)
        {
            var c = trimmed[0];
            if (c == EpsilonInternal || c == EpsilonDisplay[0] || c == EpsilonDisplayAlt[0]) return true;
        }
        return epsilonAliases.Contains(trimmed);
    }

    public static bool IsEpsilonChar(char c)
        => c == EpsilonInternal || c == EpsilonDisplay[0] || c == EpsilonDisplayAlt[0];

    public static IReadOnlyCollection<string> EpsilonAliases => [.. epsilonAliases];
}
