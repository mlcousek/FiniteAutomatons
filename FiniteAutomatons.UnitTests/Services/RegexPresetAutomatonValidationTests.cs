using FiniteAutomatons.Services.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FiniteAutomatons.UnitTests.Services;

public class RegexPresetAutomatonValidationTests
{
    private readonly RegexPresetService presetService;
    private readonly RegexToAutomatonService regexService;

    public RegexPresetAutomatonValidationTests()
    {
        presetService = new RegexPresetService();
        regexService = new RegexToAutomatonService(NullLogger<RegexToAutomatonService>.Instance);
    }

    #region Simple Literal Tests

    [Fact]
    public void SimpleLiteral_GeneratesValidAutomaton()
    {
        var preset = presetService.GetPresetByKey("simple-literal");
        preset.ShouldNotBeNull();
        ArgumentNullException.ThrowIfNull(preset);

        var enfa = regexService.BuildEpsilonNfaFromRegex(preset.Pattern);

        enfa.ShouldNotBeNull();
        enfa.States.ShouldNotBeEmpty();
        enfa.States.Count(s => s.IsStart).ShouldBe(1, "Should have exactly one start state");
        enfa.States.Count(s => s.IsAccepting).ShouldBe(1, "Should have exactly one accepting state");
    }

    [Theory]
    [InlineData("abc", true)]
    [InlineData("ab", false)]
    [InlineData("abcd", false)]
    [InlineData("", false)]
    [InlineData("a", false)]
    [InlineData("bc", false)]
    public void SimpleLiteral_AcceptsAndRejectsCorrectly(string input, bool shouldAccept)
    {
        var preset = presetService.GetPresetByKey("simple-literal");
        ArgumentNullException.ThrowIfNull(preset);
        var enfa = regexService.BuildEpsilonNfaFromRegex(preset.Pattern);

        var result = enfa.Execute(input);

        result.ShouldBe(shouldAccept, $"Input '{input}' should {(shouldAccept ? "accept" : "reject")}");
    }

    [Fact]
    public void SimpleLiteral_ValidatesAllPresetExamples()
    {
        var preset = presetService.GetPresetByKey("simple-literal");
        ArgumentNullException.ThrowIfNull(preset);
        var enfa = regexService.BuildEpsilonNfaFromRegex(preset.Pattern);

        foreach (var acceptExample in preset.AcceptExamples)
        {
            enfa.Execute(acceptExample).ShouldBeTrue($"Should accept '{acceptExample}'");
        }

        foreach (var rejectExample in preset.RejectExamples)
        {
            enfa.Execute(rejectExample).ShouldBeFalse($"Should reject '{rejectExample}'");
        }
    }

    #endregion

    #region Star Operator Tests

    [Fact]
    public void StarOperator_GeneratesValidAutomaton()
    {
        var preset = presetService.GetPresetByKey("star-operator");
        ArgumentNullException.ThrowIfNull(preset);
        var enfa = regexService.BuildEpsilonNfaFromRegex(preset.Pattern);

        enfa.ShouldNotBeNull();
        enfa.States.Count(s => s.IsStart).ShouldBe(1);
        enfa.States.Count(s => s.IsAccepting).ShouldBeGreaterThanOrEqualTo(1);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("a", true)]
    [InlineData("aa", true)]
    [InlineData("aaa", true)]
    [InlineData("aaaaaaaaaa", true)]
    [InlineData("b", false)]
    [InlineData("ab", false)]
    [InlineData("ba", false)]
    [InlineData("aab", false)]
    public void StarOperator_AcceptsAndRejectsCorrectly(string input, bool shouldAccept)
    {
        var preset = presetService.GetPresetByKey("star-operator");
        ArgumentNullException.ThrowIfNull(preset);
        var enfa = regexService.BuildEpsilonNfaFromRegex(preset.Pattern);

        enfa.Execute(input).ShouldBe(shouldAccept);
    }

    [Fact]
    public void StarOperator_ValidatesAllPresetExamples()
    {
        ValidatePresetExamples("star-operator");
    }

    #endregion

    #region Plus Operator Tests

    [Fact]
    public void PlusOperator_GeneratesValidAutomaton()
    {
        var preset = presetService.GetPresetByKey("plus-operator");
        ArgumentNullException.ThrowIfNull(preset);
        var enfa = regexService.BuildEpsilonNfaFromRegex(preset.Pattern);

        enfa.ShouldNotBeNull();
        enfa.States.Count(s => s.IsStart).ShouldBe(1);
        enfa.States.Count(s => s.IsAccepting).ShouldBeGreaterThanOrEqualTo(1);
    }

    [Theory]
    [InlineData("a", true)]
    [InlineData("aa", true)]
    [InlineData("aaa", true)]
    [InlineData("aaaaaaaaaa", true)]
    [InlineData("", false)]
    [InlineData("b", false)]
    [InlineData("ab", false)]
    [InlineData("ba", false)]
    public void PlusOperator_AcceptsAndRejectsCorrectly(string input, bool shouldAccept)
    {
        var preset = presetService.GetPresetByKey("plus-operator");
        ArgumentNullException.ThrowIfNull(preset);
        var enfa = regexService.BuildEpsilonNfaFromRegex(preset.Pattern);

        enfa.Execute(input).ShouldBe(shouldAccept);
    }

    [Fact]
    public void PlusOperator_ValidatesAllPresetExamples()
    {
        ValidatePresetExamples("plus-operator");
    }

    #endregion

    #region Alternation Tests

    [Fact]
    public void Alternation_GeneratesValidAutomaton()
    {
        var preset = presetService.GetPresetByKey("alternation");
        ArgumentNullException.ThrowIfNull(preset);
        var enfa = regexService.BuildEpsilonNfaFromRegex(preset.Pattern);

        enfa.ShouldNotBeNull();
        enfa.States.Count(s => s.IsStart).ShouldBe(1);
        enfa.States.Count(s => s.IsAccepting).ShouldBeGreaterThanOrEqualTo(1);
    }

    [Theory]
    [InlineData("a", true)]
    [InlineData("b", true)]
    [InlineData("", false)]
    [InlineData("ab", false)]
    [InlineData("ba", false)]
    [InlineData("aa", false)]
    [InlineData("bb", false)]
    [InlineData("c", false)]
    public void Alternation_AcceptsAndRejectsCorrectly(string input, bool shouldAccept)
    {
        var preset = presetService.GetPresetByKey("alternation");
        ArgumentNullException.ThrowIfNull(preset);
        var enfa = regexService.BuildEpsilonNfaFromRegex(preset.Pattern);

        enfa.Execute(input).ShouldBe(shouldAccept);
    }

    [Fact]
    public void Alternation_ValidatesAllPresetExamples()
    {
        ValidatePresetExamples("alternation");
    }

    #endregion

    #region Optional Tests

    [Fact]
    public void Optional_GeneratesValidAutomaton()
    {
        var preset = presetService.GetPresetByKey("optional");
        ArgumentNullException.ThrowIfNull(preset);
        var enfa = regexService.BuildEpsilonNfaFromRegex(preset.Pattern);

        enfa.ShouldNotBeNull();
        enfa.States.Count(s => s.IsStart).ShouldBe(1);
        enfa.States.Count(s => s.IsAccepting).ShouldBeGreaterThanOrEqualTo(1);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("a", true)]
    [InlineData("aa", false)]
    [InlineData("aaa", false)]
    [InlineData("b", false)]
    [InlineData("ab", false)]
    public void Optional_AcceptsAndRejectsCorrectly(string input, bool shouldAccept)
    {
        var preset = presetService.GetPresetByKey("optional");
        ArgumentNullException.ThrowIfNull(preset);
        var enfa = regexService.BuildEpsilonNfaFromRegex(preset.Pattern);

        enfa.Execute(input).ShouldBe(shouldAccept);
    }

    [Fact]
    public void Optional_ValidatesAllPresetExamples()
    {
        ValidatePresetExamples("optional");
    }

    #endregion

    #region Binary Strings Tests

    [Fact]
    public void BinaryStrings_GeneratesValidAutomaton()
    {
        var preset = presetService.GetPresetByKey("binary-strings");
        ArgumentNullException.ThrowIfNull(preset);
        var enfa = regexService.BuildEpsilonNfaFromRegex(preset.Pattern);

        enfa.ShouldNotBeNull();
        enfa.States.Count(s => s.IsStart).ShouldBe(1);
        enfa.States.Count(s => s.IsAccepting).ShouldBeGreaterThanOrEqualTo(1);
    }

    [Theory]
    [InlineData("01", true)]
    [InlineData("001", true)]
    [InlineData("101", true)]
    [InlineData("0001", true)]
    [InlineData("1001", true)]
    [InlineData("10101", true)]
    [InlineData("00000001", true)]
    [InlineData("11111101", true)]
    [InlineData("", false)]
    [InlineData("0", false)]
    [InlineData("1", false)]
    [InlineData("10", false)]
    [InlineData("11", false)]
    [InlineData("00", false)]
    [InlineData("010", false)]
    [InlineData("011", false)]
    public void BinaryStrings_AcceptsAndRejectsCorrectly(string input, bool shouldAccept)
    {
        var preset = presetService.GetPresetByKey("binary-strings");
        ArgumentNullException.ThrowIfNull(preset);
        var enfa = regexService.BuildEpsilonNfaFromRegex(preset.Pattern);

        enfa.Execute(input).ShouldBe(shouldAccept, $"Binary string '{input}' should {(shouldAccept ? "end with 01" : "not end with 01")}");
    }

    [Fact]
    public void BinaryStrings_ValidatesAllPresetExamples()
    {
        ValidatePresetExamples("binary-strings");
    }

    #endregion

    #region Even A's Tests

    [Fact]
    public void EvenAs_GeneratesValidAutomaton()
    {
        var preset = presetService.GetPresetByKey("even-as");
        ArgumentNullException.ThrowIfNull(preset);
        var enfa = regexService.BuildEpsilonNfaFromRegex(preset.Pattern);

        enfa.ShouldNotBeNull();
        enfa.States.Count(s => s.IsStart).ShouldBe(1);
        enfa.States.Count(s => s.IsAccepting).ShouldBeGreaterThanOrEqualTo(1);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("aa", true)]
    [InlineData("aaaa", true)]
    [InlineData("baab", true)]
    [InlineData("aabbaa", true)]
    [InlineData("bbbbaaaa", true)]
    [InlineData("a", false)]
    [InlineData("aaa", false)]
    [InlineData("aaaaa", false)]
    [InlineData("bab", false)]
    [InlineData("aabba", false)]
    public void EvenAs_AcceptsAndRejectsCorrectly(string input, bool shouldAccept)
    {
        var preset = presetService.GetPresetByKey("even-as");
        ArgumentNullException.ThrowIfNull(preset);
        var enfa = regexService.BuildEpsilonNfaFromRegex(preset.Pattern);

        var aCount = input.Count(c => c == 'a');
        enfa.Execute(input).ShouldBe(shouldAccept, $"String '{input}' has {aCount} a's (even: {aCount % 2 == 0})");
    }

    [Fact]
    public void EvenAs_ValidatesAllPresetExamples()
    {
        ValidatePresetExamples("even-as");
    }

    #endregion

    #region Character Class Tests

    [Fact]
    public void CharClass_GeneratesValidAutomaton()
    {
        var preset = presetService.GetPresetByKey("char-class");
        ArgumentNullException.ThrowIfNull(preset);
        var enfa = regexService.BuildEpsilonNfaFromRegex(preset.Pattern);

        enfa.ShouldNotBeNull();
        enfa.States.Count(s => s.IsStart).ShouldBe(1);
        enfa.States.Count(s => s.IsAccepting).ShouldBeGreaterThanOrEqualTo(1);
    }

    [Theory]
    [InlineData("a", true)]
    [InlineData("e", true)]
    [InlineData("i", true)]
    [InlineData("o", true)]
    [InlineData("u", true)]
    [InlineData("aeiou", true)]
    [InlineData("aaa", true)]
    [InlineData("eee", true)]
    [InlineData("aei", true)]
    [InlineData("", false)]
    [InlineData("b", false)]
    [InlineData("c", false)]
    [InlineData("abc", false)]
    [InlineData("aeb", false)]
    public void CharClass_AcceptsAndRejectsCorrectly(string input, bool shouldAccept)
    {
        var preset = presetService.GetPresetByKey("char-class");
        ArgumentNullException.ThrowIfNull(preset);
        var enfa = regexService.BuildEpsilonNfaFromRegex(preset.Pattern);

        enfa.Execute(input).ShouldBe(shouldAccept);
    }

    [Fact]
    public void CharClass_ValidatesAllPresetExamples()
    {
        ValidatePresetExamples("char-class");
    }

    #endregion

    #region Range Tests

    [Fact]
    public void Range_GeneratesValidAutomaton()
    {
        var preset = presetService.GetPresetByKey("range");
        ArgumentNullException.ThrowIfNull(preset);
        var enfa = regexService.BuildEpsilonNfaFromRegex(preset.Pattern);

        enfa.ShouldNotBeNull();
        enfa.States.Count(s => s.IsStart).ShouldBe(1);
        enfa.States.Count(s => s.IsAccepting).ShouldBeGreaterThanOrEqualTo(1);
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("1", true)]
    [InlineData("5", true)]
    [InlineData("9", true)]
    [InlineData("123", true)]
    [InlineData("456789", true)]
    [InlineData("0000", true)]
    [InlineData("", false)]
    [InlineData("a", false)]
    [InlineData("12a", false)]
    [InlineData("a12", false)]
    [InlineData("1a2", false)]
    public void Range_AcceptsAndRejectsCorrectly(string input, bool shouldAccept)
    {
        var preset = presetService.GetPresetByKey("range");
        ArgumentNullException.ThrowIfNull(preset);
        var enfa = regexService.BuildEpsilonNfaFromRegex(preset.Pattern);

        enfa.Execute(input).ShouldBe(shouldAccept);
    }

    [Fact]
    public void Range_ValidatesAllPresetExamples()
    {
        ValidatePresetExamples("range");
    }

    #endregion

    #region Complex (Email-like) Tests

    [Fact]
    public void Complex_GeneratesValidAutomaton()
    {
        var preset = presetService.GetPresetByKey("complex");
        ArgumentNullException.ThrowIfNull(preset);
        var enfa = regexService.BuildEpsilonNfaFromRegex(preset.Pattern);

        enfa.ShouldNotBeNull();
        enfa.States.Count(s => s.IsStart).ShouldBe(1);
        enfa.States.Count(s => s.IsAccepting).ShouldBeGreaterThanOrEqualTo(1);
    }

    [Theory]
    [InlineData("a@b", true)]
    [InlineData("user@domain", true)]
    [InlineData("abc@xyz", true)]
    [InlineData("test@test", true)]
    [InlineData("", false)]
    [InlineData("@", false)]
    [InlineData("@domain", false)]
    [InlineData("user@", false)]
    [InlineData("user", false)]
    [InlineData("domain", false)]
    [InlineData("user@@domain", false)]
    [InlineData("@user@domain", false)]
    public void Complex_AcceptsAndRejectsCorrectly(string input, bool shouldAccept)
    {
        var preset = presetService.GetPresetByKey("complex");
        ArgumentNullException.ThrowIfNull(preset);
        var enfa = regexService.BuildEpsilonNfaFromRegex(preset.Pattern);

        enfa.Execute(input).ShouldBe(shouldAccept);
    }

    [Fact]
    public void Complex_ValidatesAllPresetExamples()
    {
        ValidatePresetExamples("complex");
    }

    #endregion

    #region All Presets Validation

    [Fact]
    public void AllPresets_GenerateValidAutomatons()
    {
        var presets = presetService.GetAllPresets();

        foreach (var preset in presets)
        {
            ArgumentNullException.ThrowIfNull(preset);
            var enfa = regexService.BuildEpsilonNfaFromRegex(preset.Pattern);

            enfa.ShouldNotBeNull($"Preset '{preset.Key}' should generate automaton");
            enfa.States.ShouldNotBeEmpty($"Preset '{preset.Key}' should have states");
            enfa.States.Count(s => s.IsStart).ShouldBe(1, $"Preset '{preset.Key}' should have exactly one start state");
            enfa.States.Count(s => s.IsAccepting).ShouldBeGreaterThanOrEqualTo(1, $"Preset '{preset.Key}' should have at least one accepting state");
            enfa.Transitions.ShouldNotBeEmpty($"Preset '{preset.Key}' should have transitions");
        }
    }

    [Fact]
    public void AllPresets_ValidateAllExamples()
    {
        var presets = presetService.GetAllPresets();

        foreach (var preset in presets)
        {
            ArgumentNullException.ThrowIfNull(preset);
            ValidatePresetExamples(preset.Key);
        }
    }

    #endregion

    #region Helper Methods

    private void ValidatePresetExamples(string presetKey)
    {
        var preset = presetService.GetPresetByKey(presetKey);
        ArgumentNullException.ThrowIfNull(preset);
        preset.ShouldNotBeNull($"Preset '{presetKey}' should exist");

        var enfa = regexService.BuildEpsilonNfaFromRegex(preset.Pattern);

        foreach (var acceptExample in preset.AcceptExamples)
        {
            enfa.Execute(acceptExample).ShouldBeTrue(
                $"Preset '{presetKey}' should accept '{(acceptExample == string.Empty ? "(empty)" : acceptExample)}'");
        }

        foreach (var rejectExample in preset.RejectExamples)
        {
            enfa.Execute(rejectExample).ShouldBeFalse(
                $"Preset '{presetKey}' should reject '{(rejectExample == string.Empty ? "(empty)" : rejectExample)}'");
        }
    }

    #endregion
}
