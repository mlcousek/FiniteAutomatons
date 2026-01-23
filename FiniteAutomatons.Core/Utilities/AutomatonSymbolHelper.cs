namespace FiniteAutomatons.Core.Utilities;

public static class AutomatonSymbolHelper
{
    public const char EpsilonInternal = '\0';

    public const char EpsilonDisplay = 'ε'; // U+03B5

    public static bool IsEpsilon(string? symbol)
    {
        if (symbol is null) return false;
        var trimmed = symbol.Trim();
        if (trimmed.Length == 1)
        {
            var c = trimmed[0];
            return c == EpsilonInternal || c == EpsilonDisplay;
        }
        return false;
    }

    public static bool IsEpsilonChar(char c)
        => c == EpsilonInternal || c == EpsilonDisplay;
}
