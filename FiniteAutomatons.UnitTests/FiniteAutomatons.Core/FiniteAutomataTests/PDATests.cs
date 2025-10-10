using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using Shouldly;

namespace FiniteAutomatons.UnitTests.FiniteAutomatons.Core.FiniteAutomataTests;

public class PDATests
{
    private static PDA SimpleParenthesesPDA()
    {
        var pda = new PDA();
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = true });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = '(', StackPop = '\0', StackPush = "(" });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = ')', StackPop = '(', StackPush = null });
        return pda;
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("()", true)]
    [InlineData("(())", true)]
    [InlineData("()()", true)]
    [InlineData("(", false)]
    [InlineData(")", false)]
    [InlineData("())", false)]
    [InlineData("(()", false)]
    public void Execute_WellFormedParentheses(string input, bool expected)
    {
        var pda = SimpleParenthesesPDA();
        pda.Execute(input).ShouldBe(expected);
    }

    [Fact]
    public void Stepwise_ParenthesesProcessing()
    {
        var pda = SimpleParenthesesPDA();
        var state = pda.StartExecution("(()())");
        pda.StepForward(state); state.CurrentStateId.ShouldBe(1); state.Position.ShouldBe(1); state.IsAccepted.ShouldBeNull();
        pda.StepForward(state); state.Position.ShouldBe(2);
        pda.StepForward(state); state.Position.ShouldBe(3);
        pda.StepForward(state); state.Position.ShouldBe(4);
        pda.StepForward(state); state.Position.ShouldBe(5);
        pda.StepForward(state); state.Position.ShouldBe(6); (state.IsAccepted ?? false).ShouldBeTrue();
        pda.StepBackward(state); state.Position.ShouldBe(5); state.IsAccepted.ShouldBeNull();
    }

    [Fact]
    public void PDA_PushAndPopSequence()
    {
        var pda = new PDA();
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        pda.AddState(new State { Id = 2, IsStart = false, IsAccepting = true });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'b', StackPop = 'X', StackPush = null });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 2, Symbol = '\0', StackPop = '\0', StackPush = null });

        pda.Execute("aabb").ShouldBeTrue();
        pda.Execute("aaabbb").ShouldBeTrue();
        pda.Execute("ababb").ShouldBeFalse();
        pda.Execute("aaabb").ShouldBeFalse();
    }

    [Fact]
    public void StartExecution_InitialStackAndAcceptance()
    {
        var pda = SimpleParenthesesPDA();
        var state = pda.StartExecution("");
        state.CurrentStateId.ShouldBe(1);
        state.IsFinished.ShouldBeTrue();
        state.IsAccepted.ShouldBeNull();
        pda.ExecuteAll(state);
        (state.IsAccepted ?? false).ShouldBeTrue();
    }

    [Fact]
    public void ExecuteAll_LongBalancedString()
    {
        var pda = SimpleParenthesesPDA();
        var input = string.Concat(Enumerable.Repeat("(()())", 50));
        pda.Execute(input).ShouldBeTrue();
    }

    [Fact]
    public void BackToStart_ClearsHistoryAndStack()
    {
        var pda = SimpleParenthesesPDA();
        var state = pda.StartExecution("(()");
        pda.StepForward(state);
        pda.StepForward(state);
        state.Position.ShouldBe(2);
        var pdaState = state as PDAExecutionState;
        pdaState.ShouldNotBeNull();
        pdaState!.History.Count.ShouldBe(2);
        pda.BackToStart(state);
        state.Position.ShouldBe(0);
        pdaState.History.Count.ShouldBe(0);
    }

    [Fact]
    public void NoValidTransition_Rejection()
    {
        var pda = SimpleParenthesesPDA();
        var state = pda.StartExecution(")");
        pda.StepForward(state);
        (state.IsAccepted ?? true).ShouldBeFalse();
    }
}
