using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using Shouldly;

namespace FiniteAutomatons.UnitTests.FiniteAutomatons.Core.StateHistoryTests;

public class StateHistoryTests
{
    [Fact]
    public void DFA_StateHistory_MultipleSteps_ShouldMaintainCorrectOrder()
    {
        // Arrange: DFA with states 1->2->3->4
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: false)
            .WithState(3, isStart: false, isAccepting: false)
            .WithState(4, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .WithTransition(2, 3, 'b')
            .WithTransition(3, 4, 'c')
            .Build();

        var state = dfa.StartExecution("abc");

        // Act: Step forward 3 times
        dfa.StepForward(state); // 1->2, push 1
        dfa.StepForward(state); // 2->3, push 2
        dfa.StepForward(state); // 3->4, push 3

        // Assert: History should contain [3, 2, 1] (top to bottom)
        state.StateHistory.Count.ShouldBe(3);
        var historyArray = state.StateHistory.ToArray();
        historyArray[0].ShouldContain(3); // Most recent (top)
        historyArray[1].ShouldContain(2); // Second most recent
        historyArray[2].ShouldContain(1); // Oldest (bottom)

        // Act: Step backward 3 times
        dfa.StepBackward(state); // 4->3, pop 3
        state.CurrentStateId.ShouldBe(3);
        state.StateHistory.Count.ShouldBe(2);

        dfa.StepBackward(state); // 3->2, pop 2
        state.CurrentStateId.ShouldBe(2);
        state.StateHistory.Count.ShouldBe(1);

        dfa.StepBackward(state); // 2->1, pop 1
        state.CurrentStateId.ShouldBe(1);
        state.StateHistory.Count.ShouldBe(0);
    }

    [Fact]
    public void DFA_StateHistory_ComplexScenario_ForwardBackwardForward()
    {
        // Arrange: DFA with multiple paths
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: false)
            .WithState(3, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .WithTransition(2, 3, 'b')
            .Build();

        var state = dfa.StartExecution("ab");

        // Act: Forward twice, backward once, forward again
        dfa.StepForward(state); // 1->2, push 1
        state.CurrentStateId.ShouldBe(2);
        state.StateHistory.Count.ShouldBe(1);

        dfa.StepForward(state); // 2->3, push 2
        state.CurrentStateId.ShouldBe(3);
        state.StateHistory.Count.ShouldBe(2);

        dfa.StepBackward(state); // 3->2, pop 2
        state.CurrentStateId.ShouldBe(2);
        state.StateHistory.Count.ShouldBe(1);

        dfa.StepForward(state); // 2->3, push 2 again
        state.CurrentStateId.ShouldBe(3);
        state.StateHistory.Count.ShouldBe(2);

        // History should still be correct
        var historyArray = state.StateHistory.ToArray();
        historyArray[0].ShouldContain(2); // Most recent push
        historyArray[1].ShouldContain(1); // Original first push
    }

    [Fact]
    public void NFA_StateHistory_MultipleSteps_ShouldMaintainCorrectOrder()
    {
        // Arrange: NFA with states 1->2->3
        var nfa = new NFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: false)
            .WithState(3, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .WithTransition(2, 3, 'b')
            .Build();

        var state = nfa.StartExecution("ab");

        // Act: Step forward 2 times
        nfa.StepForward(state); // {1}->{2}, push {1}
        nfa.StepForward(state); // {2}->{3}, push {2}

        // Assert: History should contain [{2}, {1}] (top to bottom)
        state.StateHistory.Count.ShouldBe(2);
        var historyArray = state.StateHistory.ToArray();
        historyArray[0].ShouldContain(2); // Most recent (top)
        historyArray[1].ShouldContain(1); // Oldest (bottom)

        // Act: Step backward 2 times
        nfa.StepBackward(state); // {3}->{2}, pop {2}
        state.CurrentStates.ShouldNotBeNull();
        state.CurrentStates.ShouldContain(2);
        state.StateHistory.Count.ShouldBe(1);

        nfa.StepBackward(state); // {2}->{1}, pop {1}
        state.CurrentStates.ShouldNotBeNull();
        state.CurrentStates.ShouldContain(1);
        state.StateHistory.Count.ShouldBe(0);
    }

    [Fact]  
    public void EpsilonNFA_StateHistory_WithEpsilonTransitions_ShouldMaintainCorrectOrder()
    {
        // Arrange: EpsilonNFA with epsilon transitions
        var enfa = new EpsilonNFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: false)
            .WithState(3, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .WithEpsilonTransition(2, 3)
            .Build();

        var state = enfa.StartExecution("a");

        // Act: Step forward once (should reach both state 2 and 3 via epsilon)
        enfa.StepForward(state); // {1}->{2,3}, push {1}

        // Assert: Current states should include both 2 and 3
        state.CurrentStates.ShouldNotBeNull();
        state.CurrentStates.ShouldContain(2);
        state.CurrentStates.ShouldContain(3);
        state.StateHistory.Count.ShouldBe(1);
        state.StateHistory.Peek().ShouldContain(1);

        // Act: Step backward
        enfa.StepBackward(state); // {2,3}->{1}, pop {1}
        state.CurrentStates.ShouldNotBeNull();
        state.CurrentStates.ShouldContain(1);
        state.StateHistory.Count.ShouldBe(0);
    }

    [Fact]
    public void DFA_StateHistory_ManySteps_ShouldHandleCorrectly()
    {
        // Arrange: DFA with 6 states in sequence
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: false)
            .WithState(3, isStart: false, isAccepting: false)
            .WithState(4, isStart: false, isAccepting: false)
            .WithState(5, isStart: false, isAccepting: false)
            .WithState(6, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .WithTransition(2, 3, 'b')
            .WithTransition(3, 4, 'c')
            .WithTransition(4, 5, 'd')
            .WithTransition(5, 6, 'e')
            .Build();

        var state = dfa.StartExecution("abcde");

        // Act: Step forward 5 times
        for (int i = 0; i < 5; i++)
        {
            dfa.StepForward(state);
        }

        // Assert: Should be at state 6 with 5 items in history
        state.CurrentStateId.ShouldBe(6);
        state.StateHistory.Count.ShouldBe(5);

        // History should be [5, 4, 3, 2, 1] (top to bottom)
        var historyArray = state.StateHistory.ToArray();
        historyArray[0].ShouldContain(5);
        historyArray[1].ShouldContain(4);
        historyArray[2].ShouldContain(3);
        historyArray[3].ShouldContain(2);
        historyArray[4].ShouldContain(1);

        // Act: Step backward all the way to start
        for (int i = 0; i < 5; i++)
        {
            dfa.StepBackward(state);
        }

        // Assert: Should be back at state 1 with empty history
        state.CurrentStateId.ShouldBe(1);
        state.StateHistory.Count.ShouldBe(0);
        state.Position.ShouldBe(0);
    }

    [Fact]
    public void AllAutomatons_StateHistory_BackToStart_ShouldClearHistory()
    {
        // Test DFA
        var dfa = new DFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        var dfaState = dfa.StartExecution("a");
        dfa.StepForward(dfaState);
        dfaState.StateHistory.Count.ShouldBe(1);
        
        dfa.BackToStart(dfaState);
        dfaState.StateHistory.Count.ShouldBe(0);

        // Test NFA
        var nfa = new NFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        var nfaState = nfa.StartExecution("a");
        nfa.StepForward(nfaState);
        nfaState.StateHistory.Count.ShouldBe(1);
        
        nfa.BackToStart(nfaState);
        nfaState.StateHistory.Count.ShouldBe(0);

        // Test EpsilonNFA
        var enfa = new EpsilonNFABuilder()
            .WithState(1, isStart: true, isAccepting: false)
            .WithState(2, isStart: false, isAccepting: true)
            .WithTransition(1, 2, 'a')
            .Build();

        var enfaState = enfa.StartExecution("a");
        enfa.StepForward(enfaState);
        enfaState.StateHistory.Count.ShouldBe(1);
        
        enfa.BackToStart(enfaState);
        enfaState.StateHistory.Count.ShouldBe(0);
    }
}

public class DFABuilder
{
    private readonly DFA dfa = new();

    public DFABuilder WithState(int id, bool isStart = false, bool isAccepting = false)
    {
        dfa.AddState(new State { Id = id, IsStart = isStart, IsAccepting = isAccepting });
        return this;
    }

    public DFABuilder WithTransition(int fromStateId, int toStateId, char symbol)
    {
        dfa.AddTransition(fromStateId, toStateId, symbol);
        return this;
    }

    public DFA Build()
    {
        return dfa;
    }
}

public class NFABuilder
{
    private readonly NFA nfa = new();

    public NFABuilder WithState(int id, bool isStart = false, bool isAccepting = false)
    {
        nfa.AddState(new State { Id = id, IsStart = isStart, IsAccepting = isAccepting });
        return this;
    }

    public NFABuilder WithTransition(int fromStateId, int toStateId, char symbol)
    {
        nfa.AddTransition(fromStateId, toStateId, symbol);
        return this;
    }

    public NFA Build()
    {
        return nfa;
    }
}

public class EpsilonNFABuilder
{
    private readonly EpsilonNFA enfa = new();

    public EpsilonNFABuilder WithState(int id, bool isStart = false, bool isAccepting = false)
    {
        enfa.AddState(new State { Id = id, IsStart = isStart, IsAccepting = isAccepting });
        return this;
    }

    public EpsilonNFABuilder WithTransition(int fromStateId, int toStateId, char symbol)
    {
        enfa.AddTransition(fromStateId, toStateId, symbol);
        return this;
    }

    public EpsilonNFABuilder WithEpsilonTransition(int fromStateId, int toStateId)
    {
        enfa.AddEpsilonTransition(fromStateId, toStateId);
        return this;
    }

    public EpsilonNFA Build()
    {
        return enfa;
    }
}
