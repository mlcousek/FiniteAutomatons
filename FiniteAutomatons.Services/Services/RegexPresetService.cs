using FiniteAutomatons.Services.Interfaces;

namespace FiniteAutomatons.Services.Services;

public sealed class RegexPresetService : IRegexPresetService
{
    private static readonly RegexPreset[] Presets =
    [
        new("simple-literal", "Simple Literal", "abc",
            "Matches exactly 'abc'",
            ["abc"], ["ab", "abcd", ""]),

        new("star-operator", "Zero or More (a*)", "a*",
            "Matches zero or more 'a' characters",
            ["", "a", "aaa"], ["b", "ab"]),

        new("plus-operator", "One or More (a+)", "a+",
            "Matches one or more 'a' characters",
            ["a", "aaa"], ["", "b"]),

        new("alternation", "Alternation (a|b)", "a|b",
            "Matches 'a' OR 'b'",
            ["a", "b"], ["ab", "c", ""]),

        new("optional", "Optional (a?)", "a?",
            "Matches zero or one 'a'",
            ["", "a"], ["aa", "b"]),

        new("binary-strings", "Binary Ending in 01", "(0|1)*01",
            "Binary strings ending with '01'",
            ["01", "001", "10101"], ["", "10", "010"]),

        new("even-as", "Even Number of 'a'", "(b*ab*ab*)*",
            "Strings with even count of 'a'",
            ["", "aa", "baab"], ["a", "aaa"]),

        new("char-class", "Character Class [aeiou]+", "[aeiou]+",
            "One or more vowels",
            ["a", "aeiou"], ["", "b", "abc"]),

        new("range", "Digit Range [0-9]+", "[0-9]+",
            "One or more digits",
            ["1", "123"], ["", "a", "12a"]),

        new("complex", "Email-like Pattern", "[a-z]+@[a-z]+",
            "Simple email pattern",
            ["user@domain"], ["@domain", "user@", "user"])
    ];

    public IEnumerable<RegexPreset> GetAllPresets() => Presets;

    public RegexPreset? GetPresetByKey(string key) =>
        Presets.FirstOrDefault(p => p.Key == key);
}
