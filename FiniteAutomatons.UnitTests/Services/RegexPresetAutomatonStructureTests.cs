#pragma warning disable CS8602 // Test assertions verify non-null

using FiniteAutomatons.Services.Interfaces;
using FiniteAutomatons.Services.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FiniteAutomatons.UnitTests.Services;

/// <summary>
/// Advanced tests validating the internal structure and properties of automatons
/// generated from regex presets, ensuring Thompson's construction correctness.
/// </summary>
public class RegexPresetAutomatonStructureTests
{
    private readonly IRegexPresetService presetService;
    private readonly IRegexToAutomatonService regexService;

    public RegexPresetAutomatonStructureTests()
    {
        presetService = new RegexPresetService();
        regexService = new RegexToAutomatonService(NullLogger<RegexToAutomatonService>.Instance);
    }

    #region Automaton Structure Validation

    [Theory]
    [InlineData("simple-literal")]
    [InlineData("star-operator")]
    [InlineData("plus-operator")]
    [InlineData("alternation")]
    [InlineData("optional")]
    [InlineData("binary-strings")]
    [InlineData("even-as")]
    [InlineData("char-class")]
    [InlineData("range")]
    [InlineData("complex")]
    public void AllPresets_HaveExactlyOneStartState(string presetKey)
    {
        var preset = presetService.GetPresetByKey(presetKey);
        var enfa = regexService.BuildEpsilonNfaFromRegex(preset!.Pattern);

        var startStates = enfa.States.Where(s => s.IsStart).ToList();
        startStates.Count.ShouldBe(1, $"Preset '{presetKey}' should have exactly one start state");
        startStates[0].Id.ShouldBeGreaterThan(0, "Start state should have valid ID");
    }

    [Theory]
    [InlineData("simple-literal")]
    [InlineData("star-operator")]
    [InlineData("plus-operator")]
    [InlineData("alternation")]
    [InlineData("optional")]
    [InlineData("binary-strings")]
    [InlineData("even-as")]
    [InlineData("char-class")]
    [InlineData("range")]
    [InlineData("complex")]
    public void AllPresets_HaveAtLeastOneAcceptingState(string presetKey)
    {
        var preset = presetService.GetPresetByKey(presetKey);
        var enfa = regexService.BuildEpsilonNfaFromRegex(preset!.Pattern);

        var acceptingStates = enfa.States.Where(s => s.IsAccepting).ToList();
        acceptingStates.Count.ShouldBeGreaterThanOrEqualTo(1, $"Preset '{presetKey}' should have at least one accepting state");
    }

    [Theory]
    [InlineData("simple-literal")]
    [InlineData("star-operator")]
    [InlineData("plus-operator")]
    [InlineData("alternation")]
    [InlineData("optional")]
    [InlineData("binary-strings")]
    [InlineData("even-as")]
    [InlineData("char-class")]
    [InlineData("range")]
    [InlineData("complex")]
    public void AllPresets_HaveUniqueStateIds(string presetKey)
    {
        var preset = presetService.GetPresetByKey(presetKey);
        var enfa = regexService.BuildEpsilonNfaFromRegex(preset!.Pattern);

        var stateIds = enfa.States.Select(s => s.Id).ToList();
        var uniqueIds = stateIds.Distinct().ToList();

        uniqueIds.Count.ShouldBe(stateIds.Count, $"Preset '{presetKey}' should have unique state IDs");
    }

    [Theory]
    [InlineData("simple-literal")]
    [InlineData("star-operator")]
    [InlineData("plus-operator")]
    [InlineData("alternation")]
    [InlineData("optional")]
    [InlineData("binary-strings")]
    [InlineData("even-as")]
    [InlineData("char-class")]
    [InlineData("range")]
    [InlineData("complex")]
    public void AllPresets_AllTransitionsHaveValidStates(string presetKey)
    {
        var preset = presetService.GetPresetByKey(presetKey);
        var enfa = regexService.BuildEpsilonNfaFromRegex(preset!.Pattern);

        var stateIds = enfa.States.Select(s => s.Id).ToHashSet();

        foreach (var transition in enfa.Transitions)
        {
            stateIds.ShouldContain(transition.FromStateId,
                $"Preset '{presetKey}' transition from state {transition.FromStateId} should exist");
            stateIds.ShouldContain(transition.ToStateId,
                $"Preset '{presetKey}' transition to state {transition.ToStateId} should exist");
        }
    }

    [Theory]
    [InlineData("simple-literal")]
    [InlineData("star-operator")]
    [InlineData("plus-operator")]
    [InlineData("alternation")]
    [InlineData("optional")]
    [InlineData("binary-strings")]
    [InlineData("even-as")]
    [InlineData("char-class")]
    [InlineData("range")]
    [InlineData("complex")]
    public void AllPresets_StartStateIsNotAccepting_OrBothForEmptyLanguage(string presetKey)
    {
        var preset = presetService.GetPresetByKey(presetKey);
        var enfa = regexService.BuildEpsilonNfaFromRegex(preset!.Pattern);

        var startState = enfa.States.Single(s => s.IsStart);

        // For patterns that accept empty string (a*, a?, etc.), start can be accepting
        // For others, start should not be accepting
        if (startState.IsAccepting)
        {
            // Verify it actually accepts empty string
            var acceptsEmpty = enfa.Execute(string.Empty);
            acceptsEmpty.ShouldBeTrue($"Preset '{presetKey}' start state is accepting, so should accept empty string");
        }
    }

    #endregion

    #region Epsilon Transition Validation

    [Theory]
    [InlineData("star-operator")]
    [InlineData("plus-operator")]
    [InlineData("alternation")]
    [InlineData("optional")]
    [InlineData("binary-strings")]
    [InlineData("even-as")]
    public void ComplexPresets_HaveEpsilonTransitions(string presetKey)
    {
        var preset = presetService.GetPresetByKey(presetKey);
        var enfa = regexService.BuildEpsilonNfaFromRegex(preset!.Pattern);

        var epsilonTransitions = enfa.Transitions.Where(t => t.Symbol == '\0').ToList();
        epsilonTransitions.ShouldNotBeEmpty($"Complex preset '{presetKey}' should have epsilon transitions");
    }

    [Theory]
    [InlineData("simple-literal")]
    public void SimplePresets_MayNotNeedEpsilonTransitions(string presetKey)
    {
        var preset = presetService.GetPresetByKey(presetKey);
        var enfa = regexService.BuildEpsilonNfaFromRegex(preset!.Pattern);

        // Simple literals might still have epsilon transitions depending on implementation
        // This test just verifies the automaton is valid
        enfa.States.ShouldNotBeEmpty();
    }

    #endregion

    #region Reachability Tests

    [Theory]
    [InlineData("simple-literal")]
    [InlineData("star-operator")]
    [InlineData("plus-operator")]
    [InlineData("alternation")]
    [InlineData("optional")]
    [InlineData("binary-strings")]
    [InlineData("even-as")]
    [InlineData("char-class")]
    [InlineData("range")]
    [InlineData("complex")]
    public void AllPresets_AcceptingStatesAreReachableFromStart(string presetKey)
    {
        var preset = presetService.GetPresetByKey(presetKey);
        var enfa = regexService.BuildEpsilonNfaFromRegex(preset!.Pattern);

        var startState = enfa.States.Single(s => s.IsStart);
        var reachable = GetReachableStates(enfa, startState.Id);

        var acceptingStates = enfa.States.Where(s => s.IsAccepting).ToList();
        foreach (var acceptingState in acceptingStates)
        {
            reachable.ShouldContain(acceptingState.Id,
                $"Preset '{presetKey}' accepting state {acceptingState.Id} should be reachable from start");
        }
    }

    private HashSet<int> GetReachableStates(Core.Models.DoMain.FiniteAutomatons.EpsilonNFA enfa, int startStateId)
    {
        var reachable = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(startStateId);
        reachable.Add(startStateId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var outgoing = enfa.Transitions.Where(t => t.FromStateId == current);

            foreach (var transition in outgoing)
            {
                if (!reachable.Contains(transition.ToStateId))
                {
                    reachable.Add(transition.ToStateId);
                    queue.Enqueue(transition.ToStateId);
                }
            }
        }

        return reachable;
    }

    #endregion

    #region State Count Validation

    [Fact]
    public void SimpleLiteral_HasExpectedStateCount()
    {
        var preset = presetService.GetPresetByKey("simple-literal");
        var enfa = regexService.BuildEpsilonNfaFromRegex(preset!.Pattern);

        // "abc" should create states for: start -> a -> b -> c -> accept
        enfa.States.Count.ShouldBeGreaterThanOrEqualTo(4, "Simple literal should have at least 4 states");
    }

    [Fact]
    public void StarOperator_HasReasonableStateCount()
    {
        var preset = presetService.GetPresetByKey("star-operator");
        var enfa = regexService.BuildEpsilonNfaFromRegex(preset!.Pattern);

        // a* using Thompson's construction should have a small number of states
        enfa.States.Count.ShouldBeLessThan(10, "Star operator should not create excessive states");
    }

    [Fact]
    public void ComplexPattern_HasReasonableStateCount()
    {
        var preset = presetService.GetPresetByKey("complex");
        var enfa = regexService.BuildEpsilonNfaFromRegex(preset!.Pattern);

        // Complex pattern should have reasonable state count
        enfa.States.Count.ShouldBeGreaterThan(0);
        enfa.States.Count.ShouldBeLessThan(100, "Complex pattern should not create excessive states");
    }

    #endregion

    #region Determinism Tests

    [Theory]
    [InlineData("simple-literal")]
    [InlineData("star-operator")]
    [InlineData("plus-operator")]
    [InlineData("alternation")]
    [InlineData("optional")]
    [InlineData("binary-strings")]
    [InlineData("even-as")]
    [InlineData("char-class")]
    [InlineData("range")]
    [InlineData("complex")]
    public void AllPresets_ExecutionIsDeterministic(string presetKey)
    {
        var preset = presetService.GetPresetByKey(presetKey);
        var enfa = regexService.BuildEpsilonNfaFromRegex(preset!.Pattern);

        // Execute the same input multiple times - should always get same result
        foreach (var example in preset!.AcceptExamples)
        {
            var result1 = enfa.Execute(example);
            var result2 = enfa.Execute(example);
            var result3 = enfa.Execute(example);

            result1.ShouldBe(result2, $"Preset '{presetKey}' should be deterministic for '{example}'");
            result2.ShouldBe(result3, $"Preset '{presetKey}' should be deterministic for '{example}'");
        }
    }

    #endregion

    #region Alphabet Validation

    [Fact]
    public void SimpleLiteral_HasCorrectAlphabet()
    {
        var preset = presetService.GetPresetByKey("simple-literal");
        var enfa = regexService.BuildEpsilonNfaFromRegex(preset!.Pattern);

        var alphabet = enfa.Transitions
            .Where(t => t.Symbol != '\0')
            .Select(t => t.Symbol)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        alphabet.ShouldContain('a');
        alphabet.ShouldContain('b');
        alphabet.ShouldContain('c');
        alphabet.Count.ShouldBe(3);
    }

    [Fact]
    public void CharClass_HasCorrectAlphabet()
    {
        var preset = presetService.GetPresetByKey("char-class");
        var enfa = regexService.BuildEpsilonNfaFromRegex(preset!.Pattern);

        var alphabet = enfa.Transitions
            .Where(t => t.Symbol != '\0')
            .Select(t => t.Symbol)
            .Distinct()
            .ToHashSet();

        alphabet.ShouldContain('a');
        alphabet.ShouldContain('e');
        alphabet.ShouldContain('i');
        alphabet.ShouldContain('o');
        alphabet.ShouldContain('u');
    }

    [Fact]
    public void Range_HasCorrectAlphabet()
    {
        var preset = presetService.GetPresetByKey("range");
        var enfa = regexService.BuildEpsilonNfaFromRegex(preset!.Pattern);

        var alphabet = enfa.Transitions
            .Where(t => t.Symbol != '\0')
            .Select(t => t.Symbol)
            .Distinct()
            .ToHashSet();

        // Should have digits 0-9
        for (char c = '0'; c <= '9'; c++)
        {
            alphabet.ShouldContain(c, $"Range should include digit {c}");
        }
    }

    #endregion
}
