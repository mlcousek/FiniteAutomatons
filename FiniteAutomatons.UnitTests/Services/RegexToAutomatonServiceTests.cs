using FiniteAutomatons.Services.Services;
using FiniteAutomatons.Services.Interfaces;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using Shouldly;
using FiniteAutomatons.UnitTests.TestHelpers;

namespace FiniteAutomatons.UnitTests.Services;

public class RegexToAutomatonServiceTests
{
    private readonly IRegexToAutomatonService service;

    public RegexToAutomatonServiceTests()
    {
        var logger = NullLogger<RegexToAutomatonService>.Instance;
        service = new RegexToAutomatonService(logger);
    }

    [Fact]
    public void SimpleLiteralRegex_ShouldCreateEquivalentEnfa()
    {
        var enfa = service.BuildEpsilonNfaFromRegex("ab");
        enfa.ShouldNotBeNull();
        // Should accept "ab"
        enfa.Execute("ab").ShouldBeTrue();
        enfa.Execute("a").ShouldBeFalse();
        enfa.Execute("abc").ShouldBeFalse();
    }

    [Fact]
    public void AlternationAndStar_ShouldAcceptExpected()
    {
        var enfa = service.BuildEpsilonNfaFromRegex("(a|b)*c");
        enfa.Execute("c").ShouldBeTrue();
        enfa.Execute("ac").ShouldBeTrue();
        enfa.Execute("babc").ShouldBeTrue();
        enfa.Execute("").ShouldBeFalse();
        enfa.Execute("ab").ShouldBeFalse();
    }

    [Fact]
    public void PlusAndQuestionOperators_ShouldWork()
    {
        var enfa = service.BuildEpsilonNfaFromRegex("a+b?");
        enfa.Execute("a").ShouldBeTrue();
        enfa.Execute("aa").ShouldBeTrue();
        enfa.Execute("ab").ShouldBeTrue();
        enfa.Execute("b").ShouldBeFalse();
        enfa.Execute("").ShouldBeFalse();
    }

    // --- New tests for character class and range edge cases ---

    [Fact]
    public void CharacterClass_BasicMembers_ShouldAccept()
    {
        var enfa = service.BuildEpsilonNfaFromRegex("[abc]");
        enfa.Execute("a").ShouldBeTrue();
        enfa.Execute("b").ShouldBeTrue();
        enfa.Execute("c").ShouldBeTrue();
        enfa.Execute("d").ShouldBeFalse();
    }

    [Fact]
    public void CharacterRange_ShouldAcceptRangeMembers()
    {
        var enfa = service.BuildEpsilonNfaFromRegex("[a-c]+");
        enfa.Execute("a").ShouldBeTrue();
        enfa.Execute("b").ShouldBeTrue();
        enfa.Execute("cc").ShouldBeTrue();
        enfa.Execute("d").ShouldBeFalse();
    }

    [Fact]
    public void CharacterClass_WithEscapedDash_ShouldIncludeDash()
    {
        var enfa = service.BuildEpsilonNfaFromRegex(@"[\-a]");
        enfa.Execute("-").ShouldBeTrue();
        enfa.Execute("a").ShouldBeTrue();
        enfa.Execute("b").ShouldBeFalse();
    }

    [Fact]
    public void EscapedSpecialOutsideClass_ShouldBeTreatedAsLiteral()
    {
        var enfa = service.BuildEpsilonNfaFromRegex(@"a\*b");
        enfa.Execute("a*b").ShouldBeTrue();
        enfa.Execute("ab").ShouldBeFalse();
    }

    [Fact]
    public void NegatedCharacterClass_ShouldThrowNotSupported()
    {
        Should.Throw<ArgumentException>(() => service.BuildEpsilonNfaFromRegex("[^a]")).Message.ShouldContain("not supported");
    }

    [Fact]
    public void InvalidRange_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => service.BuildEpsilonNfaFromRegex("[z-a]")).Message.ShouldContain("Invalid range");
    }

    // --- Additional tests for precedence, grouping, anchors and complex patterns ---

    [Fact]
    public void ConcatenationAndAlternationPrecedence_ShouldBeCorrect()
    {
        // ab|c => (ab) | c
        var enfa = service.BuildEpsilonNfaFromRegex("ab|c");
        enfa.Execute("ab").ShouldBeTrue();
        enfa.Execute("c").ShouldBeTrue();
        enfa.Execute("a").ShouldBeFalse();
        enfa.Execute("abc").ShouldBeFalse();
    }

    [Fact]
    public void GroupingPrecedence_ShouldRespectParentheses()
    {
        var enfa = service.BuildEpsilonNfaFromRegex("(a|b)c");
        enfa.Execute("ac").ShouldBeTrue();
        enfa.Execute("bc").ShouldBeTrue();
        enfa.Execute("abc").ShouldBeFalse();
        enfa.Execute("c").ShouldBeFalse();
    }

    [Fact]
    public void AnchorsAreIgnored_ButFullMatchPreserved()
    {
        var enfa = service.BuildEpsilonNfaFromRegex("^ab$");
        // anchors are ignored by construction but automaton matches full string semantics
        enfa.Execute("ab").ShouldBeTrue();
        enfa.Execute("xab").ShouldBeFalse();
        enfa.Execute("abx").ShouldBeFalse();
    }

    [Fact]
    public void ComplexPattern_AcceptsExpectedStrings()
    {
        var enfa = service.BuildEpsilonNfaFromRegex("a(b|c)+d?");
        enfa.Execute("ab").ShouldBeTrue();
        enfa.Execute("ac").ShouldBeTrue();
        enfa.Execute("abb").ShouldBeTrue();
        enfa.Execute("acd").ShouldBeTrue();
        enfa.Execute("ad").ShouldBeFalse();
        enfa.Execute("a").ShouldBeFalse();
    }

    [Fact]
    public void MultipleRangesInClass_ShouldIncludeAllMembers()
    {
        var enfa = service.BuildEpsilonNfaFromRegex("[a-cx-z]");
        enfa.Execute("a").ShouldBeTrue();
        enfa.Execute("y").ShouldBeTrue();
        enfa.Execute("m").ShouldBeFalse();
    }

    [Fact]
    public void EscapedCharInsideClass_ShouldWork()
    {
        var enfa = service.BuildEpsilonNfaFromRegex("[\\*]");
        enfa.Execute("*").ShouldBeTrue();
        enfa.Execute("a").ShouldBeFalse();
    }

    [Fact]
    public void EmptyRegex_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => service.BuildEpsilonNfaFromRegex("")).Message.ShouldContain("Invalid regular expression");
    }

    [Fact]
    public void AStarBStarCStar_PatternMatchesExpected()
    {
        var enfa = service.BuildEpsilonNfaFromRegex("a*b*c*");

        // Should accept empty and sequences of a's then b's then c's
        var shouldAccept = new[] { "", "a", "aa", "b", "bb", "c", "ccc", "ab", "aabbb", "aaabccc", "bbbccc", "ac" };
        foreach (var s in shouldAccept)
        {
            enfa.Execute(s).ShouldBeTrue($"Expected '{s}' to be accepted by a*b*c*");
        }

        // Should reject strings with symbols out of order or interleaved
        var shouldReject = new[] { "ba", "acb", "abcabc", "cab", "bac", "bca", "abca", "abab" };
        foreach (var s in shouldReject)
        {
            enfa.Execute(s).ShouldBeFalse($"Expected '{s}' to be rejected by a*b*c*");
        }
    }

    [Fact]
    public void TrailingEscape_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => service.BuildEpsilonNfaFromRegex("a\\")).Message.ShouldContain("Trailing escape");
    }

    [Fact]
    public void EscapedAlternation_ShouldTreatPipeAsLiteral()
    {
        var enfa = service.BuildEpsilonNfaFromRegex("a\\|b");
        // Should accept string "a|b" as literal sequence
        enfa.Execute("a|b").ShouldBeTrue();
        enfa.Execute("ab").ShouldBeFalse();
    }

    [Fact]
    public void EscapedParenthesis_ShouldTreatParenthesisAsLiteral()
    {
        var enfa = service.BuildEpsilonNfaFromRegex("a\\(b\\)c");
        enfa.Execute("a(b)c").ShouldBeTrue();
        enfa.Execute("abc").ShouldBeFalse();
    }

    [Fact]
    public void CharacterClass_LeadingOrTrailingDash_ShouldIncludeDashWhenEscapedOrAtEdge()
    {
        // Leading dash is treated as literal '-'
        var enfa1 = service.BuildEpsilonNfaFromRegex("[-a]");
        enfa1.Execute("-").ShouldBeTrue();
        enfa1.Execute("a").ShouldBeTrue();

        // Trailing dash treated as literal when escaped
        var enfa2 = service.BuildEpsilonNfaFromRegex("[a\\-]");
        enfa2.Execute("-").ShouldBeTrue();
        enfa2.Execute("a").ShouldBeTrue();
    }

    [Fact]
    public void EscapedCharInsideClass_IncludingBracketAndDash_ShouldWork()
    {
        var enfa = service.BuildEpsilonNfaFromRegex("[\\]]");
        enfa.Execute("]").ShouldBeTrue();
        enfa.Execute("a").ShouldBeFalse();

        var enfa2 = service.BuildEpsilonNfaFromRegex("[\\-]");
        enfa2.Execute("-").ShouldBeTrue();
    }

    [Fact]
    public void UnicodeCharacters_ShouldBeSupportedAsLiterals()
    {
        // Use a valid Unicode character (Greek capital Omega) rather than replacement char.
        var enfa = service.BuildEpsilonNfaFromRegex("\u03A9"); // ?
        enfa.Execute("\u03A9").ShouldBeTrue(); // match same literal
        enfa.Execute("\u03A8").ShouldBeFalse(); // ? different character should not match
    }

    [Fact]
    public void ExcessivelyNestedParentheses_Unbalanced_ShouldThrow()
    {
        // Unbalanced parentheses should throw
        Should.Throw<ArgumentException>(() => service.BuildEpsilonNfaFromRegex("(((a|b)")).Message.ShouldContain("Mismatched");
    }

    [Fact]
    public void VeryDeepNesting_ShouldNotCrashButMayConstruct()
    {
        // Construct a moderately deep nested group to ensure stack handling
        var depth = 200;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < depth; i++) sb.Append('(');
        sb.Append('a');
        for (int i = 0; i < depth; i++) sb.Append(')');

        var pattern = sb.ToString();
        var enfa = service.BuildEpsilonNfaFromRegex(pattern);
        enfa.Execute("a").ShouldBeTrue();
    }

    [Fact]
    public void MultipleRangesAndOverlaps_ShouldIncludeAllMembers()
    {
        var enfa = service.BuildEpsilonNfaFromRegex("[a-ce-gx-z]");
        // should accept a, b, c, e, f, g, x, y, z
        foreach (var ch in new[] { 'a', 'b', 'c', 'e', 'f', 'g', 'x', 'y', 'z' })
        {
            enfa.Execute(ch.ToString()).ShouldBeTrue();
        }
        enfa.Execute("d").ShouldBeFalse();
        enfa.Execute("m").ShouldBeFalse();
    }

    [Fact]
    public void EscapedLeftBracket_ShouldIncludeLeftBracket()
    {
        var enfa = service.BuildEpsilonNfaFromRegex("[\\[]");
        enfa.Execute("[").ShouldBeTrue();
        enfa.Execute("a").ShouldBeFalse();
    }

    [Fact]
    public void EmptyCharacterClass_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => service.BuildEpsilonNfaFromRegex("[]")).Message.ShouldContain("Empty character class");
    }

    [Fact]
    public void DeepNesting_Stress_ShouldHandleModerateDepth()
    {
        // Use a larger depth to stress but keep it reasonable for unit test
        var depth = 500;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < depth; i++) sb.Append('(');
        sb.Append('a');
        for (int i = 0; i < depth; i++) sb.Append(')');

        var pattern = sb.ToString();
        var enfa = service.BuildEpsilonNfaFromRegex(pattern);
        enfa.Execute("a").ShouldBeTrue();
    }
}
