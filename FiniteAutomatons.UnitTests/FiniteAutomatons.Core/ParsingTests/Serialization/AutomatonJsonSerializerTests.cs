using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.Serialization;
using Shouldly;
using System.Text.Json;
using System.Collections.Generic;

namespace FiniteAutomatons.UnitTests.FiniteAutomatons.Core.ParsingTests.Serialization;

public class AutomatonJsonSerializerTests
{
    [Fact]
    public void Serialize_And_Deserialize_DFA_Works()
    {
        var dfa = new DFA();
        dfa.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        dfa.AddState(new State { Id = 2, IsStart = false, IsAccepting = true });
        dfa.AddTransition(1, 2, 'a');
        dfa.AddTransition(2, 1, 'b');
        dfa.SetStartState(1);

        var json = AutomatonJsonSerializer.Serialize(dfa);
        var deserialized = AutomatonJsonSerializer.Deserialize(json);

        deserialized.ShouldNotBeNull();
        deserialized.States.Count.ShouldBe(2);
        deserialized.Transitions.Count.ShouldBe(2);
        deserialized.States.Single(s => s.IsStart).Id.ShouldBe(1);
        deserialized.States.Single(s => s.IsAccepting).Id.ShouldBe(2);
    }

    [Fact]
    public void Serialize_DFA_IncludesTypeAndVersion()
    {
        var dfa = new DFA();
        dfa.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        dfa.AddState(new State { Id = 2, IsStart = false, IsAccepting = true });
        dfa.AddTransition(1, 2, 'a');
        dfa.AddTransition(2, 1, 'b');
        dfa.SetStartState(1);

        var json = AutomatonJsonSerializer.Serialize(dfa);
        json.ShouldContain("\"Type\":");
        json.ShouldContain("DFA");
        json.ShouldContain("\"Version\":"); // less brittle (ignore spacing)

        var roundTrip = AutomatonJsonSerializer.Deserialize(json);
        roundTrip.ShouldBeOfType<DFA>();
        roundTrip.Transitions.Count.ShouldBe(2);
    }

    [Fact]
    public void Serialize_And_Deserialize_NFA_Works()
    {
        var nfa = new NFA();
        nfa.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        nfa.AddState(new State { Id = 2, IsStart = false, IsAccepting = true });
        nfa.AddTransition(1, 1, 'a');
        nfa.AddTransition(1, 2, 'a');
        nfa.SetStartState(1);

        var json = AutomatonJsonSerializer.Serialize(nfa);
        var deserialized = AutomatonJsonSerializer.Deserialize(json);

        deserialized.ShouldNotBeNull();
        deserialized.ShouldBeOfType<NFA>();
        deserialized.States.Count.ShouldBe(2);
        deserialized.Transitions.Count.ShouldBe(2);
        deserialized.States.Single(s => s.IsStart).Id.ShouldBe(1);
        deserialized.States.Single(s => s.IsAccepting).Id.ShouldBe(2);
    }

    [Fact]
    public void Serialize_And_Deserialize_EpsilonNFA_Works()
    {
        var enfa = new EpsilonNFA();
        enfa.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        enfa.AddState(new State { Id = 2, IsStart = false, IsAccepting = true });
        enfa.AddTransition(1, 2, '\0');
        enfa.SetStartState(1);

        var json = AutomatonJsonSerializer.Serialize(enfa);

        var deserialized = AutomatonJsonSerializer.Deserialize(json);
        deserialized.ShouldBeOfType<EpsilonNFA>();
        deserialized.Transitions.Count.ShouldBe(1);
        deserialized.Transitions.Single().Symbol.ShouldBe('\0');
    }

    [Fact]
    public void Deserialize_Heuristic_WhenTypeMissing_EpsilonNFA()
    {
        var json = "{" +
                   "\"Version\":1," +
                   "\"States\":[{\"Id\":1,\"IsStart\":true,\"IsAccepting\":false},{\"Id\":2,\"IsStart\":false,\"IsAccepting\":true}]," +
                   "\"Transitions\":[{\"FromStateId\":1,\"ToStateId\":2,\"Symbol\":\"eps\"}]" +
                   "}";

        var ok = AutomatonJsonSerializer.TryDeserialize(json, out var automaton, out var error);
        ok.ShouldBeTrue(error);
        automaton.ShouldBeOfType<EpsilonNFA>();
        automaton!.Transitions.Single().Symbol.ShouldBe('\0');
    }

    [Fact]
    public void Deserialize_Heuristic_WhenTypeMissing_NFA()
    {
        var json = "{" +
                   "\"Version\":1," +
                   "\"States\":[{\"Id\":1,\"IsStart\":true,\"IsAccepting\":false},{\"Id\":2,\"IsStart\":false,\"IsAccepting\":true}]," +
                   "\"Transitions\":[{\"FromStateId\":1,\"ToStateId\":1,\"Symbol\":\"a\"},{\"FromStateId\":1,\"ToStateId\":2,\"Symbol\":\"a\"}]" +
                   "}";

        var ok = AutomatonJsonSerializer.TryDeserialize(json, out var automaton, out var error);
        ok.ShouldBeTrue(error);
        automaton.ShouldBeOfType<NFA>();
    }

    [Fact]
    public void Deserialize_Heuristic_WhenTypeMissing_DFA()
    {
        var json = "{" +
                   "\"Version\":1," +
                   "\"States\":[{\"Id\":1,\"IsStart\":true,\"IsAccepting\":false},{\"Id\":2,\"IsStart\":false,\"IsAccepting\":true}]," +
                   "\"Transitions\":[{\"FromStateId\":1,\"ToStateId\":2,\"Symbol\":\"a\"},{\"FromStateId\":2,\"ToStateId\":1,\"Symbol\":\"b\"}]" +
                   "}";
        var ok = AutomatonJsonSerializer.TryDeserialize(json, out var automaton, out var error);
        ok.ShouldBeTrue(error);
        automaton.ShouldBeOfType<DFA>();
    }

    [Fact]
    public void TryDeserialize_InvalidJson_ReturnsFalse()
    {
        var ok = AutomatonJsonSerializer.TryDeserialize("{ not-json", out var automaton, out var error);
        ok.ShouldBeFalse();
        automaton.ShouldBeNull();
        error.ShouldNotBeNull();
        error!.ToLower().ShouldContain("parse");
    }

    [Fact]
    public void TryDeserialize_NoStates_ReturnsFalse()
    {
        var json = "{\"Version\":1,\"States\":[],\"Transitions\":[]}";
        var ok = AutomatonJsonSerializer.TryDeserialize(json, out var automaton, out var error);
        ok.ShouldBeFalse();
        automaton.ShouldBeNull();
        error.ShouldNotBeNull();
        error!.ShouldContain("at least one state");
    }

    [Fact]
    public void RoundTrip_LargerAutomaton_PreservesStructure()
    {
        var dfa = new DFA();
        for (int i = 1; i <= 5; i++)
        {
            dfa.AddState(new State { Id = i, IsStart = i == 1, IsAccepting = i % 2 == 0 });
        }
        dfa.SetStartState(1);
        dfa.AddTransition(1, 2, 'a');
        dfa.AddTransition(2, 3, 'b');
        dfa.AddTransition(3, 4, 'c');
        dfa.AddTransition(4, 5, 'd');
        dfa.AddTransition(5, 1, 'e');

        var json = AutomatonJsonSerializer.Serialize(dfa);
        var copy = AutomatonJsonSerializer.Deserialize(json);

        copy.States.Count.ShouldBe(5);
        copy.Transitions.Count.ShouldBe(5);
        copy.States.Count(s => s.IsAccepting).ShouldBe(dfa.States.Count(s => s.IsAccepting));
        copy.Transitions.Select(t => (t.FromStateId, t.ToStateId, t.Symbol))
            .ShouldBeEquivalentTo(dfa.Transitions.Select(t => (t.FromStateId, t.ToStateId, t.Symbol)));
    }
}
