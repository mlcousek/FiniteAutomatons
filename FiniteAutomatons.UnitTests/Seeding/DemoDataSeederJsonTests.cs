using FiniteAutomatons.Core.Models.DTOs;
using FiniteAutomatons.Core.Models.Serialization;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Data.Seeding;
using Shouldly;
using System.Text.Json;

namespace FiniteAutomatons.UnitTests.Seeding;

public class DemoDataSeederJsonTests
{
    private static AutomatonPayloadDto Deserialize(string contentJson)
    {
        var dto = JsonSerializer.Deserialize<AutomatonPayloadDto>(contentJson);
        dto.ShouldNotBeNull("ContentJson must deserialize to AutomatonPayloadDto");
        return dto;
    }

    private static AutomatonPayloadDto Convert(string automatonJson)
    {
        var contentJson = DemoDataSeeder.ConvertToContentJson(automatonJson);
        return Deserialize(contentJson);
    }

    // ──────────────────── DFA tests ────────────────────

    [Fact]
    public void DfaEvenAs_ParsesAndConverts()
    {
        var dto = Convert(DemoDataSeeder.DfaEvenAsJson);
        dto.Type.ShouldBe(AutomatonType.DFA);
        dto.States!.Count.ShouldBe(2);
        dto.States.Count(s => s.IsStart).ShouldBe(1);
        dto.States.Count(s => s.IsAccepting).ShouldBe(1);
        dto.Transitions!.Count.ShouldBe(4);
        dto.Transitions.ShouldAllBe(t => t.StackPop == null && t.StackPush == null);
    }

    [Fact]
    public void DfaBinaryDiv3_ParsesAndConverts()
    {
        var dto = Convert(DemoDataSeeder.DfaBinaryDiv3Json);
        dto.Type.ShouldBe(AutomatonType.DFA);
        dto.States!.Count.ShouldBe(3);
        dto.Transitions!.Count.ShouldBe(6);
    }

    [Fact]
    public void DfaEndsInB_ParsesAndConverts()
    {
        var dto = Convert(DemoDataSeeder.DfaEndsInBJson);
        dto.Type.ShouldBe(AutomatonType.DFA);
        dto.States!.Count.ShouldBe(2);
        dto.Transitions!.Count.ShouldBe(4);
        dto.States.Count(s => s.IsAccepting).ShouldBe(1);
    }

    [Fact]
    public void DfaStartsWithAMinimizable_ParsesAndConverts()
    {
        var dto = Convert(DemoDataSeeder.DfaStartsWithAMinimizableJson);
        dto.Type.ShouldBe(AutomatonType.DFA);
        dto.States!.Count.ShouldBe(4);
        dto.Transitions!.Count.ShouldBe(8);
    }

    [Fact]
    public void DfaAcceptAll_ParsesAndConverts()
    {
        var dto = Convert(DemoDataSeeder.DfaAcceptAllJson);
        dto.Type.ShouldBe(AutomatonType.DFA);
        dto.States!.Count.ShouldBe(1);
        dto.States[0].IsStart.ShouldBeTrue();
        dto.States[0].IsAccepting.ShouldBeTrue();
        dto.Transitions!.Count.ShouldBe(2);
    }

    [Fact]
    public void DfaAlternating_ParsesAndConverts()
    {
        var dto = Convert(DemoDataSeeder.DfaAlternatingJson);
        dto.Type.ShouldBe(AutomatonType.DFA);
        dto.States!.Count.ShouldBe(4);
        dto.Transitions!.Count.ShouldBe(8);
    }

    [Fact]
    public void DfaNoConsecAs_ParsesAndConverts()
    {
        var dto = Convert(DemoDataSeeder.DfaNoConsecAsJson);
        dto.Type.ShouldBe(AutomatonType.DFA);
        dto.States!.Count.ShouldBe(3);
        dto.Transitions!.Count.ShouldBe(6);
        dto.States.Count(s => s.IsAccepting).ShouldBe(2);
    }

    [Fact]
    public void DfaEvenLength_ParsesAndConverts()
    {
        var dto = Convert(DemoDataSeeder.DfaEvenLengthJson);
        dto.Type.ShouldBe(AutomatonType.DFA);
        dto.States!.Count.ShouldBe(2);
        dto.Transitions!.Count.ShouldBe(4);
    }

    [Fact]
    public void DfaExactlyTwoAs_ParsesAndConverts()
    {
        var dto = Convert(DemoDataSeeder.DfaExactlyTwoAsJson);
        dto.Type.ShouldBe(AutomatonType.DFA);
        dto.States!.Count.ShouldBe(4);
        dto.Transitions!.Count.ShouldBe(8);
        dto.States.Count(s => s.IsAccepting).ShouldBe(1);
    }

    [Fact]
    public void DfaLengthDivBy3_ParsesAndConverts()
    {
        var dto = Convert(DemoDataSeeder.DfaLengthDivBy3Json);
        dto.Type.ShouldBe(AutomatonType.DFA);
        dto.States!.Count.ShouldBe(3);
        dto.Transitions!.Count.ShouldBe(3);
        dto.States.Count(s => s.IsAccepting).ShouldBe(1);
    }

    // ──────────────────── NFA tests ────────────────────

    [Fact]
    public void NfaContainsAb_ParsesAndConverts()
    {
        var dto = Convert(DemoDataSeeder.NfaContainsAbJson);
        dto.Type.ShouldBe(AutomatonType.NFA);
        dto.States!.Count.ShouldBe(3);
        dto.Transitions!.Count.ShouldBe(6);
        dto.Transitions.ShouldAllBe(t => t.Symbol != '\0');
    }

    [Fact]
    public void NfaSecondToLastA_ParsesAndConverts()
    {
        var dto = Convert(DemoDataSeeder.NfaSecondToLastAJson);
        dto.Type.ShouldBe(AutomatonType.NFA);
        dto.States!.Count.ShouldBe(3);
        dto.Transitions!.Count.ShouldBe(5);
    }

    [Fact]
    public void NfaStartsWithAb_ParsesAndConverts()
    {
        var dto = Convert(DemoDataSeeder.NfaStartsWithAbJson);
        dto.Type.ShouldBe(AutomatonType.NFA);
        dto.States!.Count.ShouldBe(3);
        dto.Transitions!.Count.ShouldBe(4);
    }

    [Fact]
    public void NfaEndsInAb_ParsesAndConverts()
    {
        var dto = Convert(DemoDataSeeder.NfaEndsInAbJson);
        dto.Type.ShouldBe(AutomatonType.NFA);
        dto.States!.Count.ShouldBe(3);
        dto.Transitions!.Count.ShouldBe(4);
        dto.States.Count(s => s.IsAccepting).ShouldBe(1);
    }

    [Fact]
    public void NfaContainsAba_ParsesAndConverts()
    {
        var dto = Convert(DemoDataSeeder.NfaContainsAbaJson);
        dto.Type.ShouldBe(AutomatonType.NFA);
        dto.States!.Count.ShouldBe(4);
        dto.Transitions!.Count.ShouldBe(7);
    }

    // ──────────────────── ε-NFA tests ────────────────────

    [Fact]
    public void EnfaABorC_ParsesAndConverts()
    {
        var dto = Convert(DemoDataSeeder.EnfaABorCJson);
        dto.Type.ShouldBe(AutomatonType.EpsilonNFA);
        dto.States!.Count.ShouldBe(5);
        dto.Transitions!.Count.ShouldBe(5);
        // Two epsilon transitions
        dto.Transitions.Count(t => t.Symbol == '\0').ShouldBe(2);
    }

    [Fact]
    public void EnfaStarB_ParsesAndConverts()
    {
        var dto = Convert(DemoDataSeeder.EnfaStarBJson);
        dto.Type.ShouldBe(AutomatonType.EpsilonNFA);
        dto.States!.Count.ShouldBe(4);
        dto.Transitions!.Count.ShouldBe(4);
        dto.Transitions.Count(t => t.Symbol == '\0').ShouldBe(2);
    }

    [Fact]
    public void EnfaOptionalAB_ParsesAndConverts()
    {
        var dto = Convert(DemoDataSeeder.EnfaOptionalABJson);
        dto.Type.ShouldBe(AutomatonType.EpsilonNFA);
        dto.States!.Count.ShouldBe(3);
        dto.Transitions!.Count.ShouldBe(3);
        dto.Transitions.Count(t => t.Symbol == '\0').ShouldBe(1);
    }

    // ──────────────────── DPDA tests ────────────────────

    [Fact]
    public void DpdaAnBn_ParsesAndConverts()
    {
        var dto = Convert(DemoDataSeeder.DpdaAnBnJson);
        dto.Type.ShouldBe(AutomatonType.DPDA);
        dto.States!.Count.ShouldBe(2);
        dto.Transitions!.Count.ShouldBe(3);
        // Push transitions use StackPush
        dto.Transitions.Count(t => t.StackPush != null).ShouldBe(1);
    }

    [Fact]
    public void DpdaBalancedParens_ParsesAndConverts()
    {
        var dto = Convert(DemoDataSeeder.DpdaBalancedParensJson);
        dto.Type.ShouldBe(AutomatonType.DPDA);
        dto.States!.Count.ShouldBe(1);
        dto.Transitions!.Count.ShouldBe(2);
        dto.Transitions.Count(t => t.StackPush != null).ShouldBe(1);
        dto.Transitions.Count(t => t.StackPop.HasValue && t.StackPop.Value == '(').ShouldBe(1);
    }

    [Fact]
    public void DpdaAtLeastAsManyAs_ParsesAndConverts()
    {
        var dto = Convert(DemoDataSeeder.DpdaAtLeastAsManyAsJson);
        dto.Type.ShouldBe(AutomatonType.DPDA);
        dto.States!.Count.ShouldBe(2);
        dto.Transitions!.Count.ShouldBe(3);
    }

    // ──────────────────── NPDA tests ────────────────────

    [Fact]
    public void NpdaEvenPalindromes_ParsesAndConverts()
    {
        var dto = Convert(DemoDataSeeder.NpdaEvenPalindromesJson);
        dto.Type.ShouldBe(AutomatonType.NPDA);
        dto.States!.Count.ShouldBe(2);
        dto.Transitions!.Count.ShouldBe(5);
        // Midpoint epsilon transition
        dto.Transitions.Count(t => t.Symbol == '\0').ShouldBe(1);
    }

    // ──────────────────── ContentJson round-trip ────────────────────

    [Theory]
    [InlineData(nameof(AutomatonType.DFA), 2, 4)]  // DfaEvenAs
    public void ConvertToContentJson_ProducesValidJson(string typeName, int expectedStates, int expectedTransitions)
    {
        _ = typeName; // used for readability in test name
        var contentJson = DemoDataSeeder.ConvertToContentJson(DemoDataSeeder.DfaEvenAsJson);
        contentJson.ShouldNotBeNullOrWhiteSpace();

        var dto = JsonSerializer.Deserialize<AutomatonPayloadDto>(contentJson);
        dto.ShouldNotBeNull();
        dto.States!.Count.ShouldBe(expectedStates);
        dto.Transitions!.Count.ShouldBe(expectedTransitions);
    }

    [Fact]
    public void AllDemoJsonConstants_AutomatonJsonSerializerCanParse()
    {
        // Ensure every raw JSON constant is valid AutomatonJsonSerializer format
        var jsonConstants = new[]
        {
            DemoDataSeeder.DfaEvenAsJson,
            DemoDataSeeder.DfaBinaryDiv3Json,
            DemoDataSeeder.NfaContainsAbJson,
            DemoDataSeeder.DfaEndsInBJson,
            DemoDataSeeder.DfaStartsWithAMinimizableJson,
            DemoDataSeeder.EnfaABorCJson,
            DemoDataSeeder.DpdaAnBnJson,
            DemoDataSeeder.DpdaBalancedParensJson,
            DemoDataSeeder.NpdaEvenPalindromesJson,
            DemoDataSeeder.DfaAcceptAllJson,
            DemoDataSeeder.NfaSecondToLastAJson,
            DemoDataSeeder.NfaStartsWithAbJson,
            DemoDataSeeder.EnfaStarBJson,
            DemoDataSeeder.DfaAlternatingJson,
            DemoDataSeeder.DfaNoConsecAsJson,
            DemoDataSeeder.NfaEndsInAbJson,
            DemoDataSeeder.DfaEvenLengthJson,
            DemoDataSeeder.DfaExactlyTwoAsJson,
            DemoDataSeeder.DpdaAtLeastAsManyAsJson,
            DemoDataSeeder.NfaContainsAbaJson,
            DemoDataSeeder.DfaLengthDivBy3Json,
            DemoDataSeeder.EnfaOptionalABJson,
        };

        foreach (var json in jsonConstants)
        {
            AutomatonJsonSerializer.TryDeserialize(json, out var automaton, out var error).ShouldBeTrue(error);
            automaton.ShouldNotBeNull();
        }
    }
}
