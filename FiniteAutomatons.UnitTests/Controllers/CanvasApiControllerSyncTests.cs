using FiniteAutomatons.Controllers;
using FiniteAutomatons.Core.Models.Api;
using FiniteAutomatons.UnitTests.TestHelpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shouldly;

namespace FiniteAutomatons.UnitTests.Controllers;

public class CanvasApiControllerSyncTests
{
    private readonly CanvasApiController controller;

    public CanvasApiControllerSyncTests()
    {
        var logger = new NoOpLogger<CanvasApiController>();
        controller = new CanvasApiController(logger)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public void Sync_NullRequest_ReturnsBadRequest()
    {
        var result = controller.Sync(null, null);
        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void Sync_EmptyRequest_ReturnsOkWithEmptyData()
    {
        var req = new CanvasSyncRequest { Type = "DFA", States = [], Transitions = [] };
        var result = controller.Sync(req, null) as OkObjectResult;
        result.ShouldNotBeNull();
        var resp = result.Value as CanvasSyncResponse;
        resp.ShouldNotBeNull();
        resp.Alphabet.ShouldBeEmpty();
        resp.States.ShouldBeEmpty();
        resp.Transitions.ShouldBeEmpty();
    }

    [Fact]
    public void Sync_DFA_SingleSymbol_AlphabetContainsThatSymbol()
    {
        var req = BuildDfaRequest(
            states: [new() { Id = 0, IsStart = true }, new() { Id = 1, IsAccepting = true }],
            transitions: [new() { FromStateId = 0, ToStateId = 1, Symbol = "a" }]);

        var resp = GetResponse(req);
        resp.Alphabet.ShouldContain("a");
        resp.Alphabet.Count.ShouldBe(1);
    }

    [Fact]
    public void Sync_DFA_MultipleDistinctSymbols_AlphabetHasAll()
    {
        var req = BuildDfaRequest(
            states: [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            transitions: [
                new() { FromStateId = 0, ToStateId = 1, Symbol = "a" },
                new() { FromStateId = 0, ToStateId = 0, Symbol = "b" },
                new() { FromStateId = 1, ToStateId = 0, Symbol = "c" }
            ]);

        var resp = GetResponse(req);
        resp.Alphabet.ShouldContain("a");
        resp.Alphabet.ShouldContain("b");
        resp.Alphabet.ShouldContain("c");
        resp.Alphabet.Count.ShouldBe(3);
    }

    [Fact]
    public void Sync_DFA_DuplicateSymbolTransitions_AlphabetHasDistinct()
    {
        var req = BuildDfaRequest(
            states: [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            transitions: [
                new() { FromStateId = 0, ToStateId = 1, Symbol = "a" },
                new() { FromStateId = 1, ToStateId = 0, Symbol = "a" }
            ]);

        var resp = GetResponse(req);
        resp.Alphabet.Count.ShouldBe(1);
        resp.Alphabet.ShouldContain("a");
    }

    [Fact]
    public void Sync_EpsilonTransition_NotInAlphabet()
    {
        var req = BuildRequest("EpsilonNFA",
            states: [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            transitions: [new() { FromStateId = 0, ToStateId = 1, Symbol = "\\0" }]);

        var resp = GetResponse(req);
        resp.Alphabet.ShouldBeEmpty();
        resp.HasEpsilonTransitions.ShouldBeTrue();
    }

    [Fact]
    public void Sync_MixedEpsilonAndRegularSymbols_AlphabetExcludesEpsilon()
    {
        var req = BuildRequest("EpsilonNFA",
            states: [new() { Id = 0, IsStart = true }, new() { Id = 1 }, new() { Id = 2, IsAccepting = true }],
            transitions: [
                new() { FromStateId = 0, ToStateId = 1, Symbol = "a" },
                new() { FromStateId = 1, ToStateId = 2, Symbol = "\\0" }
            ]);

        var resp = GetResponse(req);
        resp.Alphabet.ShouldContain("a");
        resp.Alphabet.ShouldNotContain("ε");
        resp.HasEpsilonTransitions.ShouldBeTrue();
    }

    [Fact]
    public void Sync_AlphabetIsSortedAlphabetically()
    {
        var req = BuildDfaRequest(
            states: [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            transitions: [
                new() { FromStateId = 0, ToStateId = 1, Symbol = "c" },
                new() { FromStateId = 1, ToStateId = 0, Symbol = "a" },
                new() { FromStateId = 0, ToStateId = 0, Symbol = "b" }
            ]);

        var resp = GetResponse(req);
        resp.Alphabet.ShouldBe([.. resp.Alphabet.OrderBy(x => x)]);
    }

    [Fact]
    public void Sync_FiveDistinctSymbols_AlphabetCountFive()
    {
        var symbols = new[] { "a", "b", "c", "d", "e" };
        var states = new List<CanvasSyncState> { new() { Id = 0, IsStart = true }, new() { Id = 1 } };
        var transitions = symbols.Select(s => new CanvasSyncTransition { FromStateId = 0, ToStateId = 1, Symbol = s }).ToList();
        var req = new CanvasSyncRequest { Type = "NFA", States = states, Transitions = transitions };

        var resp = GetResponse(req);
        resp.Alphabet.Count.ShouldBe(5);
        foreach (var sym in symbols) resp.Alphabet.ShouldContain(sym);
    }

    [Fact]
    public void Sync_NoTransitions_AlphabetEmpty()
    {
        var req = BuildDfaRequest(
            states: [new() { Id = 0, IsStart = true }],
            transitions: []);

        GetResponse(req).Alphabet.ShouldBeEmpty();
    }

    [Fact]
    public void Sync_States_CountMatchesInput()
    {
        var req = BuildDfaRequest(
            states: [new() { Id = 0, IsStart = true }, new() { Id = 1 }, new() { Id = 2, IsAccepting = true }],
            transitions: []);

        var resp = GetResponse(req);
        resp.StateCount.ShouldBe(3);
        resp.States.Count.ShouldBe(3);
    }

    [Fact]
    public void Sync_States_StartStateFlagPreserved()
    {
        var req = BuildDfaRequest(
            states: [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            transitions: []);

        var resp = GetResponse(req);
        resp.States.Single(s => s.Id == 0).IsStart.ShouldBeTrue();
        resp.States.Single(s => s.Id == 1).IsStart.ShouldBeFalse();
    }

    [Fact]
    public void Sync_States_AcceptingStateFlagPreserved()
    {
        var req = BuildDfaRequest(
            states: [new() { Id = 0, IsStart = true }, new() { Id = 1, IsAccepting = true }],
            transitions: []);

        var resp = GetResponse(req);
        resp.States.Single(s => s.Id == 1).IsAccepting.ShouldBeTrue();
        resp.States.Single(s => s.Id == 0).IsAccepting.ShouldBeFalse();
    }

    [Fact]
    public void Sync_States_SortedById()
    {
        var req = BuildDfaRequest(
            states: [new() { Id = 5 }, new() { Id = 2 }, new() { Id = 0, IsStart = true }],
            transitions: []);

        var resp = GetResponse(req);
        resp.States.Select(s => s.Id).ShouldBe([0, 2, 5]);
    }

    [Fact]
    public void Sync_SingleState_NoTransitions_ValidResponse()
    {
        var req = BuildDfaRequest(
            states: [new() { Id = 0, IsStart = true, IsAccepting = true }],
            transitions: []);

        var resp = GetResponse(req);
        resp.StateCount.ShouldBe(1);
        resp.TransitionCount.ShouldBe(0);
    }

    [Fact]
    public void Sync_States_LabelDerivedFromId()
    {
        var req = BuildDfaRequest(
            states: [new() { Id = 42, IsStart = true }],
            transitions: []);

        var resp = GetResponse(req);
        resp.States[0].Label.ShouldBe("q42");
    }

    [Fact]
    public void Sync_StateCountEquals_StatesListCount()
    {
        var req = BuildDfaRequest(
            states: [new() { Id = 0, IsStart = true }, new() { Id = 1 }, new() { Id = 2 }],
            transitions: []);

        var resp = GetResponse(req);
        resp.StateCount.ShouldBe(resp.States.Count);
    }

    [Fact]
    public void Sync_Transitions_CountMatchesInput()
    {
        var req = BuildDfaRequest(
            states: [new() { Id = 0, IsStart = true }, new() { Id = 1 }, new() { Id = 2 }],
            transitions: [
                new() { FromStateId = 0, ToStateId = 1, Symbol = "a" },
                new() { FromStateId = 1, ToStateId = 2, Symbol = "b" }
            ]);

        var resp = GetResponse(req);
        resp.TransitionCount.ShouldBe(2);
        resp.Transitions.Count.ShouldBe(2);
    }

    [Fact]
    public void Sync_Transitions_SymbolDisplayCorrect()
    {
        var req = BuildDfaRequest(
            states: [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            transitions: [new() { FromStateId = 0, ToStateId = 1, Symbol = "x" }]);

        var resp = GetResponse(req);
        resp.Transitions[0].SymbolDisplay.ShouldBe("x");
    }

    [Fact]
    public void Sync_Transitions_EpsilonSymbolDisplayedAsEpsilonChar()
    {
        var req = BuildRequest("EpsilonNFA",
            states: [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            transitions: [new() { FromStateId = 0, ToStateId = 1, Symbol = "\\0" }]);

        var resp = GetResponse(req);
        resp.Transitions[0].SymbolDisplay.ShouldBe("ε");
    }

    [Fact]
    public void Sync_Transitions_SortedByFromStateId()
    {
        var req = BuildDfaRequest(
            states: [new() { Id = 0, IsStart = true }, new() { Id = 1 }, new() { Id = 2 }],
            transitions: [
                new() { FromStateId = 2, ToStateId = 0, Symbol = "b" },
                new() { FromStateId = 0, ToStateId = 1, Symbol = "a" }
            ]);

        var resp = GetResponse(req);
        resp.Transitions[0].FromStateId.ShouldBe(0);
        resp.Transitions[1].FromStateId.ShouldBe(2);
    }

    [Fact]
    public void Sync_Transitions_FromAndToStateIdsPreserved()
    {
        var req = BuildDfaRequest(
            states: [new() { Id = 3, IsStart = true }, new() { Id = 7 }],
            transitions: [new() { FromStateId = 3, ToStateId = 7, Symbol = "z" }]);

        var resp = GetResponse(req);
        resp.Transitions[0].FromStateId.ShouldBe(3);
        resp.Transitions[0].ToStateId.ShouldBe(7);
    }

    [Fact]
    public void Sync_SelfLoopTransition_RecordedCorrectly()
    {
        var req = BuildDfaRequest(
            states: [new() { Id = 0, IsStart = true, IsAccepting = true }],
            transitions: [new() { FromStateId = 0, ToStateId = 0, Symbol = "a" }]);

        var resp = GetResponse(req);
        resp.Transitions[0].FromStateId.ShouldBe(0);
        resp.Transitions[0].ToStateId.ShouldBe(0);
    }

    [Fact]
    public void Sync_TransitionCountEquals_TransitionsListCount()
    {
        var req = BuildDfaRequest(
            states: [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            transitions: [new() { FromStateId = 0, ToStateId = 1, Symbol = "a" }]);

        var resp = GetResponse(req);
        resp.TransitionCount.ShouldBe(resp.Transitions.Count);
    }

    [Theory]
    [InlineData("DFA", false)]
    [InlineData("NFA", false)]
    [InlineData("EpsilonNFA", false)]
    [InlineData("PDA", true)]
    public void Sync_IsPDA_CorrectForType(string type, bool expectedIsPDA)
    {
        var req = BuildRequest(type,
            states: [new() { Id = 0, IsStart = true }],
            transitions: []);

        var resp = GetResponse(req);
        resp.IsPDA.ShouldBe(expectedIsPDA);
    }

    [Theory]
    [InlineData("DFA")]
    [InlineData("NFA")]
    [InlineData("EpsilonNFA")]
    [InlineData("PDA")]
    [InlineData("dfa")]
    [InlineData("nfa")]
    [InlineData("pda")]
    public void Sync_AllSupportedTypes_ReturnOk(string type)
    {
        var req = BuildRequest(type,
            states: [new() { Id = 0, IsStart = true }],
            transitions: []);

        var result = controller.Sync(req, null);
        result.ShouldBeOfType<OkObjectResult>();
    }

    [Fact]
    public void Sync_PDA_StackPopDisplayed()
    {
        var req = BuildRequest("PDA",
            states: [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            transitions: [new() { FromStateId = 0, ToStateId = 1, Symbol = "a", StackPop = "Z", StackPush = "AZ" }]);

        var resp = GetResponse(req);
        resp.Transitions[0].StackPopDisplay.ShouldBe("Z");
        resp.Transitions[0].StackPush.ShouldBe("AZ");
    }

    [Fact]
    public void Sync_PDA_EpsilonStackPopDisplayedAsEpsilonChar()
    {
        var req = BuildRequest("PDA",
            states: [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            transitions: [new() { FromStateId = 0, ToStateId = 1, Symbol = "a", StackPop = "\\0", StackPush = "Z" }]);

        var resp = GetResponse(req);
        resp.Transitions[0].StackPopDisplay.ShouldBe("ε");
    }

    [Fact]
    public void Sync_PDA_TransitionMarkedAsPDA()
    {
        var req = BuildRequest("PDA",
            states: [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            transitions: [new() { FromStateId = 0, ToStateId = 1, Symbol = "a", StackPop = "Z", StackPush = "" }]);

        var resp = GetResponse(req);
        resp.Transitions[0].IsPDA.ShouldBeTrue();
    }

    [Fact]
    public void Sync_NonPDA_StackPopNullInResponse()
    {
        var req = BuildDfaRequest(
            states: [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            transitions: [new() { FromStateId = 0, ToStateId = 1, Symbol = "a" }]);

        var resp = GetResponse(req);
        resp.Transitions[0].StackPopDisplay.ShouldBeNull();
        resp.Transitions[0].IsPDA.ShouldBeFalse();
    }

    [Fact]
    public void Sync_PDA_StackPushEmpty_StoredAsEmpty()
    {
        var req = BuildRequest("PDA",
            states: [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            transitions: [new() { FromStateId = 0, ToStateId = 1, Symbol = "a", StackPop = "Z", StackPush = null }]);

        var resp = GetResponse(req);
        resp.Transitions[0].StackPush.ShouldBe("");
    }

    [Fact]
    public void Sync_PDA_MultipleTransitions_AllMarkedAsPDA()
    {
        var req = BuildRequest("PDA",
            states: [new() { Id = 0, IsStart = true }, new() { Id = 1 }, new() { Id = 2 }],
            transitions: [
                new() { FromStateId = 0, ToStateId = 1, Symbol = "a", StackPop = "Z", StackPush = "AZ" },
                new() { FromStateId = 1, ToStateId = 2, Symbol = "b", StackPop = "A", StackPush = "" }
            ]);

        var resp = GetResponse(req);
        resp.Transitions.ShouldAllBe(t => t.IsPDA);
    }

    [Fact]
    public void Sync_NoEpsilonTransitions_FlagFalse()
    {
        var req = BuildDfaRequest(
            states: [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            transitions: [new() { FromStateId = 0, ToStateId = 1, Symbol = "a" }]);

        var resp = GetResponse(req);
        resp.HasEpsilonTransitions.ShouldBeFalse();
    }

    [Fact]
    public void Sync_EpsilonSymbol_FlagTrue()
    {
        var req = BuildRequest("EpsilonNFA",
            states: [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            transitions: [new() { FromStateId = 0, ToStateId = 1, Symbol = "ε" }]);

        var resp = GetResponse(req);
        resp.HasEpsilonTransitions.ShouldBeTrue();
    }

    [Fact]
    public void Sync_BackslashZeroSymbol_TreatedAsEpsilon()
    {
        var req = BuildRequest("EpsilonNFA",
            states: [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            transitions: [new() { FromStateId = 0, ToStateId = 1, Symbol = "\\0" }]);

        var resp = GetResponse(req);
        resp.HasEpsilonTransitions.ShouldBeTrue();
        resp.Alphabet.ShouldBeEmpty();
    }

    [Fact]
    public void Sync_EpsilonKeyword_TreatedAsEpsilon()
    {
        var req = BuildRequest("EpsilonNFA",
            states: [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            transitions: [new() { FromStateId = 0, ToStateId = 1, Symbol = "epsilon" }]);

        var resp = GetResponse(req);
        resp.HasEpsilonTransitions.ShouldBeTrue();
        resp.Alphabet.ShouldBeEmpty();
    }

    [Fact]
    public void Sync_LargeAutomaton_AllStatesAndTransitionsReturned()
    {
        var states = Enumerable.Range(0, 20)
            .Select(i => new CanvasSyncState { Id = i, IsStart = i == 0, IsAccepting = i == 19 })
            .ToList();
        var transitions = Enumerable.Range(0, 19)
            .Select(i => new CanvasSyncTransition { FromStateId = i, ToStateId = i + 1, Symbol = "a" })
            .ToList();

        var req = new CanvasSyncRequest { Type = "DFA", States = states, Transitions = transitions };
        var resp = GetResponse(req);

        resp.StateCount.ShouldBe(20);
        resp.TransitionCount.ShouldBe(19);
    }

    [Fact]
    public void Sync_NFA_MultipleTransitionsFromSameState_AllReturned()
    {
        var req = BuildRequest("NFA",
            states: [new() { Id = 0, IsStart = true }, new() { Id = 1 }, new() { Id = 2 }],
            transitions: [
                new() { FromStateId = 0, ToStateId = 1, Symbol = "a" },
                new() { FromStateId = 0, ToStateId = 2, Symbol = "a" }
            ]);

        var resp = GetResponse(req);
        resp.Transitions.Count.ShouldBe(2);
        resp.Alphabet.Count.ShouldBe(1);
    }

    [Fact]
    public void Sync_NFA_IsPDAFalse()
    {
        var req = BuildRequest("NFA",
            states: [new() { Id = 0, IsStart = true }],
            transitions: []);
        GetResponse(req).IsPDA.ShouldBeFalse();
    }

    [Fact]
    public void Sync_Response_ContainsAllRequiredFields()
    {
        var req = BuildDfaRequest(
            states: [new() { Id = 0, IsStart = true, IsAccepting = true }],
            transitions: []);

        var resp = GetResponse(req);
        resp.Alphabet.ShouldNotBeNull();
        resp.States.ShouldNotBeNull();
        resp.Transitions.ShouldNotBeNull();
        resp.StateCount.ShouldBeGreaterThanOrEqualTo(0);
        resp.TransitionCount.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Sync_NoStates_ReturnsZeroCounts()
    {
        var req = new CanvasSyncRequest { Type = "DFA", States = [], Transitions = [] };
        var resp = GetResponse(req);
        resp.StateCount.ShouldBe(0);
        resp.TransitionCount.ShouldBe(0);
    }

    [Theory]
    [InlineData("\\0", "ε")]
    [InlineData("ε", "ε")]
    [InlineData("epsilon", "ε")]
    [InlineData("a", "a")]
    [InlineData("0", "0")]
    [InlineData("1", "1")]
    [InlineData("z", "z")]
    [InlineData("A", "A")]
    public void Sync_SymbolNormalization_CorrectDisplay(string rawSymbol, string expectedDisplay)
    {
        var req = BuildRequest("EpsilonNFA",
            states: [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            transitions: [new() { FromStateId = 0, ToStateId = 1, Symbol = rawSymbol }]);

        var resp = GetResponse(req);
        resp.Transitions[0].SymbolDisplay.ShouldBe(expectedDisplay);
    }

    private CanvasSyncResponse GetResponse(CanvasSyncRequest req)
    {
        var result = controller.Sync(req, null) as OkObjectResult;
        result.ShouldNotBeNull();
        var resp = result.Value as CanvasSyncResponse;
        resp.ShouldNotBeNull();
        return resp;
    }

    private static CanvasSyncRequest BuildDfaRequest(
        List<CanvasSyncState> states,
        List<CanvasSyncTransition> transitions)
        => BuildRequest("DFA", states, transitions);

    private static CanvasSyncRequest BuildRequest(string type,
        List<CanvasSyncState> states,
        List<CanvasSyncTransition> transitions)
        => new() { Type = type, States = states, Transitions = transitions };
}
