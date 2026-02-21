using FiniteAutomatons.Core.Models.Api;
using Shouldly;

namespace FiniteAutomatons.UnitTests.Controllers;

public class CanvasSyncRequestModelTests
{
    // ── CanvasSyncRequest defaults ────────────────────────────────────

    [Fact]
    public void CanvasSyncRequest_DefaultType_IsDFA()
    {
        var req = new CanvasSyncRequest();
        req.Type.ShouldBe("DFA");
    }

    [Fact]
    public void CanvasSyncRequest_DefaultStates_IsEmptyList()
        => new CanvasSyncRequest().States.ShouldNotBeNull();

    [Fact]
    public void CanvasSyncRequest_DefaultTransitions_IsEmptyList()
        => new CanvasSyncRequest().Transitions.ShouldNotBeNull();

    [Fact]
    public void CanvasSyncRequest_TypeSet_Preserved()
        => new CanvasSyncRequest { Type = "PDA" }.Type.ShouldBe("PDA");

    [Theory]
    [InlineData("DFA")]
    [InlineData("NFA")]
    [InlineData("EpsilonNFA")]
    [InlineData("PDA")]
    public void CanvasSyncRequest_KnownTypes_CanBeSet(string type)
        => new CanvasSyncRequest { Type = type }.Type.ShouldBe(type);

    // ── CanvasSyncState ───────────────────────────────────────────────

    [Fact]
    public void CanvasSyncState_DefaultIsStart_False()
        => new CanvasSyncState().IsStart.ShouldBeFalse();

    [Fact]
    public void CanvasSyncState_DefaultIsAccepting_False()
        => new CanvasSyncState().IsAccepting.ShouldBeFalse();

    [Fact]
    public void CanvasSyncState_DefaultId_Zero()
        => new CanvasSyncState().Id.ShouldBe(0);

    [Fact]
    public void CanvasSyncState_SetId_Preserved()
        => new CanvasSyncState { Id = 42 }.Id.ShouldBe(42);

    [Fact]
    public void CanvasSyncState_AllFlags_Settable()
    {
        var s = new CanvasSyncState { Id = 1, IsStart = true, IsAccepting = true };
        s.IsStart.ShouldBeTrue();
        s.IsAccepting.ShouldBeTrue();
    }

    [Fact]
    public void CanvasSyncState_OnlyIsStart_AcceptingNotSet()
    {
        var s = new CanvasSyncState { IsStart = true };
        s.IsAccepting.ShouldBeFalse();
    }

    // ── CanvasSyncTransition ──────────────────────────────────────────

    [Fact]
    public void CanvasSyncTransition_DefaultSymbol_Empty()
        => new CanvasSyncTransition().Symbol.ShouldBe(string.Empty);

    [Fact]
    public void CanvasSyncTransition_DefaultStackPop_IsNull()
        => new CanvasSyncTransition().StackPop.ShouldBeNull();

    [Fact]
    public void CanvasSyncTransition_DefaultStackPush_IsNull()
        => new CanvasSyncTransition().StackPush.ShouldBeNull();

    [Fact]
    public void CanvasSyncTransition_SetFromTo_Preserved()
    {
        var t = new CanvasSyncTransition { FromStateId = 3, ToStateId = 7 };
        t.FromStateId.ShouldBe(3);
        t.ToStateId.ShouldBe(7);
    }

    [Fact]
    public void CanvasSyncTransition_SetSymbol_Preserved()
        => new CanvasSyncTransition { Symbol = "x" }.Symbol.ShouldBe("x");

    [Fact]
    public void CanvasSyncTransition_SetStackOps_Preserved()
    {
        var t = new CanvasSyncTransition { StackPop = "Z", StackPush = "AZ" };
        t.StackPop.ShouldBe("Z");
        t.StackPush.ShouldBe("AZ");
    }

    [Fact]
    public void CanvasSyncTransition_SelfLoop_Allowed()
    {
        var t = new CanvasSyncTransition { FromStateId = 5, ToStateId = 5, Symbol = "a" };
        t.FromStateId.ShouldBe(t.ToStateId);
    }

    // ── CanvasSyncResponse defaults ───────────────────────────────────

    [Fact]
    public void CanvasSyncResponse_DefaultAlphabet_NotNull()
        => new CanvasSyncResponse().Alphabet.ShouldNotBeNull();

    [Fact]
    public void CanvasSyncResponse_DefaultAlphabet_Empty()
        => new CanvasSyncResponse().Alphabet.ShouldBeEmpty();

    [Fact]
    public void CanvasSyncResponse_DefaultStates_NotNull()
        => new CanvasSyncResponse().States.ShouldNotBeNull();

    [Fact]
    public void CanvasSyncResponse_DefaultTransitions_NotNull()
        => new CanvasSyncResponse().Transitions.ShouldNotBeNull();

    [Fact]
    public void CanvasSyncResponse_DefaultHasEpsilon_False()
        => new CanvasSyncResponse().HasEpsilonTransitions.ShouldBeFalse();

    [Fact]
    public void CanvasSyncResponse_DefaultIsPDA_False()
        => new CanvasSyncResponse().IsPDA.ShouldBeFalse();

    [Fact]
    public void CanvasSyncResponse_DefaultStateCounts_Zero()
    {
        var r = new CanvasSyncResponse();
        r.StateCount.ShouldBe(0);
        r.TransitionCount.ShouldBe(0);
    }

    // ── CanvasSyncStateDto ────────────────────────────────────────────

    [Fact]
    public void CanvasSyncStateDto_Label_DerivedFromId()
        => new CanvasSyncStateDto { Id = 5 }.Label.ShouldBe("q5");

    [Fact]
    public void CanvasSyncStateDto_Label_ZeroId()
        => new CanvasSyncStateDto { Id = 0 }.Label.ShouldBe("q0");

    [Fact]
    public void CanvasSyncStateDto_DefaultIsStart_False()
        => new CanvasSyncStateDto().IsStart.ShouldBeFalse();

    [Fact]
    public void CanvasSyncStateDto_DefaultIsAccepting_False()
        => new CanvasSyncStateDto().IsAccepting.ShouldBeFalse();

    [Fact]
    public void CanvasSyncStateDto_SetFlags_Preserved()
    {
        var dto = new CanvasSyncStateDto { Id = 1, IsStart = true, IsAccepting = true };
        dto.IsStart.ShouldBeTrue();
        dto.IsAccepting.ShouldBeTrue();
    }

    // ── CanvasSyncTransitionDto ───────────────────────────────────────

    [Fact]
    public void CanvasSyncTransitionDto_DefaultStackPop_Null()
        => new CanvasSyncTransitionDto().StackPopDisplay.ShouldBeNull();

    [Fact]
    public void CanvasSyncTransitionDto_DefaultIsPDA_False()
        => new CanvasSyncTransitionDto().IsPDA.ShouldBeFalse();

    [Fact]
    public void CanvasSyncTransitionDto_DefaultSymbolDisplay_Empty()
        => new CanvasSyncTransitionDto().SymbolDisplay.ShouldBe(string.Empty);

    [Fact]
    public void CanvasSyncTransitionDto_SetFields_Preserved()
    {
        var t = new CanvasSyncTransitionDto
        {
            FromStateId = 1,
            ToStateId = 2,
            SymbolDisplay = "a",
            IsPDA = true,
            StackPopDisplay = "Z",
            StackPush = "XZ"
        };
        t.FromStateId.ShouldBe(1);
        t.ToStateId.ShouldBe(2);
        t.SymbolDisplay.ShouldBe("a");
        t.IsPDA.ShouldBeTrue();
        t.StackPopDisplay.ShouldBe("Z");
        t.StackPush.ShouldBe("XZ");
    }

    [Fact]
    public void CanvasSyncTransitionDto_DefaultStackPush_Null()
        => new CanvasSyncTransitionDto().StackPush.ShouldBeNull();
}
