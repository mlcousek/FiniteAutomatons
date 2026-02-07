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

        deserialized.ShouldBeOfType<DFA>();
        deserialized.States.Count.ShouldBe(2);
        deserialized.Transitions.Count.ShouldBe(2);
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

        deserialized.ShouldBeOfType<NFA>();
        deserialized.Transitions.Count.ShouldBe(2);
    }

    [Fact]
    public void Serialize_And_Deserialize_EpsilonNFA_Works()
    {
        var enfa = new EpsilonNFA();
        enfa.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        enfa.AddState(new State { Id = 2, IsStart = false, IsAccepting = true });
        enfa.AddTransition(1, 2, '\0');
        enfa.SetStartState(1);

        var text = AutomatonCustomTextSerializer.Serialize(enfa);
        var deserialized = AutomatonCustomTextSerializer.Deserialize(text);

        deserialized.ShouldBeOfType<EpsilonNFA>();
        deserialized.Transitions.Single().Symbol.ShouldBe('\0');
    }

    [Fact]
    public void TryDeserialize_EmptyInput_Fails()
    {
        var ok = AutomatonCustomTextSerializer.TryDeserialize("", out var automaton, out var errors);
        ok.ShouldBeFalse();
        automaton.ShouldBeNull();
        errors.ShouldContain(e => e.Contains("empty", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TryDeserialize_MultipleInitialStates_Fails()
    {
        string text = @"$states:
            q0
            q1

            $initial:
            q0
            q1

            $accepting:
            q1

            $transitions:
            q0:a>q1";
        var ok = AutomatonCustomTextSerializer.TryDeserialize(text, out var automaton, out var errors);
        ok.ShouldBeFalse();
        errors.ShouldContain(e => e.Contains("Multiple initial", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TryDeserialize_InitialStateUndefined_Fails()
    {
        string text = @"$states:
                q0

                $initial:
                q1

                $accepting:
                q0

                $transitions:
                q0:a>q0";
        var ok = AutomatonCustomTextSerializer.TryDeserialize(text, out _, out var errors);
        ok.ShouldBeFalse();
        errors.ShouldContain(e => e.Contains("Initial state 'q1'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TryDeserialize_AcceptingStateUndefined_Fails()
    {
        string text = @"$states:
            q0

            $initial:
            q0

            $accepting:
            q1

            $transitions:
            q0:a>q0";
        var ok = AutomatonCustomTextSerializer.TryDeserialize(text, out _, out var errors);
        ok.ShouldBeFalse();
        errors.ShouldContain(e => e.Contains("Accepting state 'q1'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TryDeserialize_InvalidTransitionSyntax_Fails()
    {
        string text = @"$states:
                q0
                q1

                $initial:
                q0

                $accepting:
                q1

                $transitions:
                q0:a>q1
                INVALID_LINE";
        var ok = AutomatonCustomTextSerializer.TryDeserialize(text, out _, out var errors);
        ok.ShouldBeFalse();
        errors.ShouldContain(e => e.Contains("Invalid transition syntax", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TryDeserialize_TransitionUnknownState_Fails()
    {
        string text = @"$states:
                q0
                q1

                $initial:
                q0

                $accepting:
                q1

                $transitions:
                q0:a>q2"; // q2 undefined
        var ok = AutomatonCustomTextSerializer.TryDeserialize(text, out _, out var errors);
        ok.ShouldBeFalse();
        errors.ShouldContain(e => e.Contains("unknown 'to' state 'q2'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TryDeserialize_MultiCharNonEpsilonSymbol_Fails()
    {
        string text = @"$states:
                q0
                q1

                $initial:
                q0

                $accepting:
                q1

                $transitions:
                q0:ab>q1"; // invalid symbol
        var ok = AutomatonCustomTextSerializer.TryDeserialize(text, out _, out var errors);
        ok.ShouldBeFalse();
        errors.ShouldContain(e => e.Contains("must be a single character", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TryDeserialize_TypeInference_NFA()
    {
        string text = @"$states:
                q0
                q1

                $initial:
                q0

                $accepting:
                q1

                $transitions:
                q0:a>q0
                q0:a>q1"; // nondeterministic on 'a'
        var ok = AutomatonCustomTextSerializer.TryDeserialize(text, out var automaton, out var errors);
        ok.ShouldBeTrue(string.Join(';', errors));
        automaton.ShouldBeOfType<NFA>();
    }

    [Fact]
    public void TryDeserialize_TypeInference_EpsilonNFA()
    {
        string text = @"$states:
                q0
                q1

                $initial:
                q0

                $accepting:
                q1

                $transitions:
                q0:ε>q1";
        var ok = AutomatonCustomTextSerializer.TryDeserialize(text, out var automaton, out var errors);
        ok.ShouldBeTrue(string.Join(';', errors));
        automaton.ShouldBeOfType<EpsilonNFA>();
        automaton!.Transitions.Single().Symbol.ShouldBe('\0');
    }

    [Fact]
    public void TryDeserialize_TypeInference_DFA()
    {
        string text = @"$states:
                q0
                q1

                $initial:
                q0

                $accepting:
                q1

                $transitions:
                q0:a>q1
                q1:b>q0";
        var ok = AutomatonCustomTextSerializer.TryDeserialize(text, out var automaton, out var errors);
        ok.ShouldBeTrue(string.Join(';', errors));
        automaton.ShouldBeOfType<DFA>();
    }

    [Fact]
    public void Deserialize_IgnoresCommentsAndBlankLines()
    {
        string text = @"# This is a DFA example
                $states:
                q0
                q1

                # initial state
                $initial:
                q0

                $accepting:
                # none accepting except q1
                q1

                $transitions:
                # transition lines
                q0:a>q1
                q1:b>q0
                ";
        var ok = AutomatonCustomTextSerializer.TryDeserialize(text, out var automaton, out var errors);
        ok.ShouldBeTrue(string.Join(';', errors));
        automaton!.States.Count.ShouldBe(2);
        automaton.Transitions.Count.ShouldBe(2);
    }

    [Fact]
    public void RoundTrip_PreservesBehavior()
    {
        string original = @"$states:
                q0
                q1
                q2

                $initial:
                q0

                $accepting:
                q2

                $transitions:
                q0:a>q1
                q1:b>q2
                q2:a>q2";
        var ok = AutomatonCustomTextSerializer.TryDeserialize(original, out var automaton, out var errors);
        ok.ShouldBeTrue(string.Join(';', errors));
        var serialized = AutomatonCustomTextSerializer.Serialize(automaton!);
        var second = AutomatonCustomTextSerializer.Deserialize(serialized); // second parse should succeed
        second.States.Count.ShouldBe(3);
        second.Transitions.Count.ShouldBe(3);
    }
}
