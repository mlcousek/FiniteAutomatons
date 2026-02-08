using FiniteAutomatons.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Net;
using System.Text.Json;

namespace FiniteAutomatons.IntegrationTests.RegexApiTests;

/// <summary>
/// End-to-end integration tests validating that regex presets work correctly
/// through the entire application stack (API ? Service ? Automaton ? Execution).
/// </summary>
[Collection("Integration Tests")]
public class RegexPresetsIntegrationTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    #region Simple Literal Integration Tests

    [Theory]
    [InlineData("abc", true)]
    [InlineData("ab", false)]
    [InlineData("abcd", false)]
    [InlineData("", false)]
    public async Task SimpleLiteral_BuildAndExecute_ShouldMatchExpected(string input, bool shouldAccept)
    {
        await ValidateRegexPreset("simple-literal", input, shouldAccept);
    }

    [Fact]
    public async Task SimpleLiteral_ValidatesAllPresetExamples()
    {
        await ValidateAllExamplesForPreset("simple-literal");
    }

    #endregion

    #region Star Operator Integration Tests

    [Theory]
    [InlineData("", true)]
    [InlineData("a", true)]
    [InlineData("aa", true)]
    [InlineData("aaa", true)]
    [InlineData("aaaaaaaaaa", true)]
    [InlineData("b", false)]
    [InlineData("ab", false)]
    [InlineData("aab", false)]
    public async Task StarOperator_BuildAndExecute_ShouldMatchExpected(string input, bool shouldAccept)
    {
        await ValidateRegexPreset("star-operator", input, shouldAccept);
    }

    [Fact]
    public async Task StarOperator_ValidatesAllPresetExamples()
    {
        await ValidateAllExamplesForPreset("star-operator");
    }

    #endregion

    #region Plus Operator Integration Tests

    [Theory]
    [InlineData("a", true)]
    [InlineData("aa", true)]
    [InlineData("aaa", true)]
    [InlineData("aaaaaaaaaa", true)]
    [InlineData("", false)]
    [InlineData("b", false)]
    [InlineData("ab", false)]
    public async Task PlusOperator_BuildAndExecute_ShouldMatchExpected(string input, bool shouldAccept)
    {
        await ValidateRegexPreset("plus-operator", input, shouldAccept);
    }

    [Fact]
    public async Task PlusOperator_ValidatesAllPresetExamples()
    {
        await ValidateAllExamplesForPreset("plus-operator");
    }

    #endregion

    #region Alternation Integration Tests

    [Theory]
    [InlineData("a", true)]
    [InlineData("b", true)]
    [InlineData("", false)]
    [InlineData("ab", false)]
    [InlineData("c", false)]
    [InlineData("aa", false)]
    public async Task Alternation_BuildAndExecute_ShouldMatchExpected(string input, bool shouldAccept)
    {
        await ValidateRegexPreset("alternation", input, shouldAccept);
    }

    [Fact]
    public async Task Alternation_ValidatesAllPresetExamples()
    {
        await ValidateAllExamplesForPreset("alternation");
    }

    #endregion

    #region Optional Integration Tests

    [Theory]
    [InlineData("", true)]
    [InlineData("a", true)]
    [InlineData("aa", false)]
    [InlineData("b", false)]
    public async Task Optional_BuildAndExecute_ShouldMatchExpected(string input, bool shouldAccept)
    {
        await ValidateRegexPreset("optional", input, shouldAccept);
    }

    [Fact]
    public async Task Optional_ValidatesAllPresetExamples()
    {
        await ValidateAllExamplesForPreset("optional");
    }

    #endregion

    #region Binary Strings Integration Tests

    [Theory]
    [InlineData("01", true)]
    [InlineData("001", true)]
    [InlineData("101", true)]
    [InlineData("10101", true)]
    [InlineData("0001", true)]
    [InlineData("111101", true)]
    [InlineData("", false)]
    [InlineData("0", false)]
    [InlineData("1", false)]
    [InlineData("10", false)]
    [InlineData("00", false)]
    [InlineData("010", false)]
    public async Task BinaryStrings_BuildAndExecute_ShouldMatchExpected(string input, bool shouldAccept)
    {
        await ValidateRegexPreset("binary-strings", input, shouldAccept);
    }

    [Fact]
    public async Task BinaryStrings_ValidatesAllPresetExamples()
    {
        await ValidateAllExamplesForPreset("binary-strings");
    }

    #endregion

    #region Even A's Integration Tests

    [Theory]
    [InlineData("", true)]
    [InlineData("aa", true)]
    [InlineData("aaaa", true)]
    [InlineData("baab", true)]
    [InlineData("bbbbaaaa", true)]
    [InlineData("a", false)]
    [InlineData("aaa", false)]
    [InlineData("bab", false)]
    public async Task EvenAs_BuildAndExecute_ShouldMatchExpected(string input, bool shouldAccept)
    {
        await ValidateRegexPreset("even-as", input, shouldAccept);
    }

    [Fact]
    public async Task EvenAs_ValidatesAllPresetExamples()
    {
        await ValidateAllExamplesForPreset("even-as");
    }

    #endregion

    #region Character Class Integration Tests

    [Theory]
    [InlineData("a", true)]
    [InlineData("e", true)]
    [InlineData("i", true)]
    [InlineData("o", true)]
    [InlineData("u", true)]
    [InlineData("aeiou", true)]
    [InlineData("", false)]
    [InlineData("b", false)]
    [InlineData("abc", false)]
    public async Task CharClass_BuildAndExecute_ShouldMatchExpected(string input, bool shouldAccept)
    {
        await ValidateRegexPreset("char-class", input, shouldAccept);
    }

    [Fact]
    public async Task CharClass_ValidatesAllPresetExamples()
    {
        await ValidateAllExamplesForPreset("char-class");
    }

    #endregion

    #region Range Integration Tests

    [Theory]
    [InlineData("0", true)]
    [InlineData("1", true)]
    [InlineData("9", true)]
    [InlineData("123", true)]
    [InlineData("456789", true)]
    [InlineData("", false)]
    [InlineData("a", false)]
    [InlineData("12a", false)]
    public async Task Range_BuildAndExecute_ShouldMatchExpected(string input, bool shouldAccept)
    {
        await ValidateRegexPreset("range", input, shouldAccept);
    }

    [Fact]
    public async Task Range_ValidatesAllPresetExamples()
    {
        await ValidateAllExamplesForPreset("range");
    }

    #endregion

    #region Complex Integration Tests

    [Theory]
    [InlineData("a@b", true)]
    [InlineData("user@domain", true)]
    [InlineData("abc@xyz", true)]
    [InlineData("", false)]
    [InlineData("@domain", false)]
    [InlineData("user@", false)]
    [InlineData("user", false)]
    public async Task Complex_BuildAndExecute_ShouldMatchExpected(string input, bool shouldAccept)
    {
        await ValidateRegexPreset("complex", input, shouldAccept);
    }

    [Fact]
    public async Task Complex_ValidatesAllPresetExamples()
    {
        await ValidateAllExamplesForPreset("complex");
    }

    #endregion

    #region All Presets Integration Tests

    [Fact]
    public async Task AllPresets_CanBeRetrievedViaAPI()
    {
        var client = GetHttpClient();
        var response = await client.GetAsync("/Regex/GetPresets");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
        doc.RootElement.GetArrayLength().ShouldBe(10, "Should have exactly 10 presets");
    }

    [Fact]
    public async Task AllPresets_BuildValidAutomatonsViaService()
    {
        using var scope = GetServiceScope();
        var presetService = scope.ServiceProvider.GetRequiredService<IRegexPresetService>();
        var regexService = scope.ServiceProvider.GetRequiredService<IRegexToAutomatonService>();

        var presets = presetService.GetAllPresets();

        foreach (var preset in presets)
        {
            var enfa = regexService.BuildEpsilonNfaFromRegex(preset.Pattern);

            enfa.ShouldNotBeNull($"Preset '{preset.Key}' should generate automaton");
            enfa.States.Count(s => s.IsStart).ShouldBe(1, $"Preset '{preset.Key}' should have one start state");
            enfa.States.Count(s => s.IsAccepting).ShouldBeGreaterThanOrEqualTo(1, $"Preset '{preset.Key}' should have accepting states");
        }
    }

    [Fact]
    public async Task AllPresets_ValidateAllExamples()
    {
        using var scope = GetServiceScope();
        var presetService = scope.ServiceProvider.GetRequiredService<IRegexPresetService>();

        var presets = presetService.GetAllPresets();

        foreach (var preset in presets)
        {
            await ValidateAllExamplesForPreset(preset.Key);
        }
    }

    #endregion

    #region Cross-Preset Edge Cases

    [Theory]
    [InlineData("simple-literal", "ABC", false)] // Case sensitivity
    [InlineData("simple-literal", " abc", false)] // Leading whitespace
    [InlineData("simple-literal", "abc ", false)] // Trailing whitespace
    [InlineData("star-operator", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)] // Very long input
    [InlineData("binary-strings", "000000000000000000000000000000001", true)] // Long binary
    [InlineData("char-class", "AEIOU", false)] // Uppercase vowels
    [InlineData("range", "00000000000000000", true)] // Many zeros
    [InlineData("complex", "A@B", false)] // Uppercase in email
    public async Task EdgeCases_HandleCorrectly(string presetKey, string input, bool shouldAccept)
    {
        await ValidateRegexPreset(presetKey, input, shouldAccept);
    }

    #endregion

    #region Helper Methods

    private async Task ValidateRegexPreset(string presetKey, string input, bool shouldAccept)
    {
        using var scope = GetServiceScope();
        var presetService = scope.ServiceProvider.GetRequiredService<IRegexPresetService>();
        var regexService = scope.ServiceProvider.GetRequiredService<IRegexToAutomatonService>();

        var preset = presetService.GetPresetByKey(presetKey);
        preset.ShouldNotBeNull($"Preset '{presetKey}' should exist");

        var enfa = regexService.BuildEpsilonNfaFromRegex(preset.Pattern);
        var result = enfa.Execute(input);

        result.ShouldBe(shouldAccept,
            $"Preset '{presetKey}' with pattern '{preset.Pattern}' should {(shouldAccept ? "accept" : "reject")} input '{(input == string.Empty ? "(empty)" : input)}'");
    }

    private async Task ValidateAllExamplesForPreset(string presetKey)
    {
        using var scope = GetServiceScope();
        var presetService = scope.ServiceProvider.GetRequiredService<IRegexPresetService>();
        var regexService = scope.ServiceProvider.GetRequiredService<IRegexToAutomatonService>();

        var preset = presetService.GetPresetByKey(presetKey);
        preset.ShouldNotBeNull($"Preset '{presetKey}' should exist");

        var enfa = regexService.BuildEpsilonNfaFromRegex(preset.Pattern);

        // Validate accept examples
        foreach (var acceptExample in preset.AcceptExamples)
        {
            var result = enfa.Execute(acceptExample);
            result.ShouldBeTrue(
                $"Preset '{presetKey}' should accept example '{(acceptExample == string.Empty ? "(empty)" : acceptExample)}'");
        }

        // Validate reject examples
        foreach (var rejectExample in preset.RejectExamples)
        {
            var result = enfa.Execute(rejectExample);
            result.ShouldBeFalse(
                $"Preset '{presetKey}' should reject example '{(rejectExample == string.Empty ? "(empty)" : rejectExample)}'");
        }

        await Task.CompletedTask;
    }

    #endregion
}
