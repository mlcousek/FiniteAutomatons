using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.Serialization;
using Shouldly;

namespace FiniteAutomatons.UnitTests.FiniteAutomatons.Core.ParsingTests.Serialization;

public class AutomatonCustomTextSerializerTests
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

        var text = AutomatonCustomTextSerializer.Serialize(dfa);
        var deserialized = AutomatonCustomTextSerializer.Deserialize(text);

        deserialized.ShouldNotBeNull();
        deserialized.States.Count.ShouldBe(2);
        deserialized.Transitions.Count.ShouldBe(2);
        deserialized.States.Single(s => s.IsStart).Id.ShouldBe(1);
        deserialized.States.Single(s => s.IsAccepting).Id.ShouldBe(2);
    }

    [Fact]
    public void Serialize_And_Deserialize_NFA_Works()
    {
        var nfa = new NFA();
        nfa.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        nfa.AddState(new State { Id = 2, IsStart = false, IsAccepting = true });
        nfa.AddTransition(1, 1, 'a');
        nfa.AddTransition(1, 2, 'a'); // nondeterministic
        nfa.SetStartState(1);

        var text = AutomatonCustomTextSerializer.Serialize(nfa);
        var deserialized = AutomatonCustomTextSerializer.Deserialize(text);

        deserialized.ShouldNotBeNull();
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
        enfa.AddTransition(1, 2, '\0'); // epsilon
        enfa.SetStartState(1);

        var text = AutomatonCustomTextSerializer.Serialize(enfa);
        var deserialized = AutomatonCustomTextSerializer.Deserialize(text);

        deserialized.ShouldNotBeNull();
        deserialized.States.Count.ShouldBe(2);
        deserialized.Transitions.Count.ShouldBe(1);
        deserialized.States.Single(s => s.IsStart).Id.ShouldBe(1);
        deserialized.States.Single(s => s.IsAccepting).Id.ShouldBe(2);
    }
}
