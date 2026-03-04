using FiniteAutomatons.Controllers;
using FiniteAutomatons.Core.Models.Api;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.UnitTests.TestHelpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using System.Text;
using System.Text.Json;

namespace FiniteAutomatons.UnitTests.Controllers;

public class CanvasApiControllerBuildViewModelTests
{
    private static (CanvasApiController ctrl, MockSession session) CreateController()
    {
        var ctrl = new CanvasApiController(new NoOpLogger<CanvasApiController>(), new MockCanvasMappingService(), new MockAutomatonMinimizationService());
        var session = new MockSession();
        var httpContext = new DefaultHttpContext { Session = session };
        ctrl.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return (ctrl, session);
    }

    private static AutomatonViewModel SaveAndRead(CanvasApiController ctrl, MockSession session, CanvasSyncRequest req)
    {
        ctrl.Save(req);
        session.TryGetValue(CanvasApiController.SessionKey, out var bytes);
        var json = Encoding.UTF8.GetString(bytes);
        return JsonSerializer.Deserialize<AutomatonViewModel>(json)!;
    }

    [Theory]
    [InlineData("DFA", AutomatonType.DFA)]
    [InlineData("dfa", AutomatonType.DFA)]
    [InlineData("NFA", AutomatonType.NFA)]
    [InlineData("nfa", AutomatonType.NFA)]
    [InlineData("EpsilonNFA", AutomatonType.EpsilonNFA)]
    [InlineData("EPSILONNFA", AutomatonType.EpsilonNFA)]
    [InlineData("PDA", AutomatonType.PDA)]
    [InlineData("pda", AutomatonType.PDA)]
    [InlineData("unknown_type", AutomatonType.DFA)]
    [InlineData("", AutomatonType.DFA)]
    public void BuildViewModel_TypeMapping_AllCases(string rawType, AutomatonType expected)
    {
        var (ctrl, session) = CreateController();
        var model = SaveAndRead(ctrl, session, new CanvasSyncRequest { Type = rawType, States = [], Transitions = [] });
        model.Type.ShouldBe(expected);
    }

    [Theory]
    [InlineData("a", 'a')]
    [InlineData("b", 'b')]
    [InlineData("z", 'z')]
    [InlineData("0", '0')]
    [InlineData("9", '9')]
    [InlineData("A", 'A')]
    [InlineData("X", 'X')]
    public void BuildViewModel_ParseSymbol_SingleChar(string raw, char expected)
    {
        var (ctrl, session) = CreateController();
        var model = SaveAndRead(ctrl, session, Req("DFA", raw));
        model.Transitions[0].Symbol.ShouldBe(expected);
    }

    [Theory]
    [InlineData("\\0")]
    [InlineData("ε")]
    [InlineData("epsilon")]
    [InlineData("")]
    public void BuildViewModel_ParseSymbol_EpsilonVariants_MappedToNulChar(string raw)
    {
        var (ctrl, session) = CreateController();
        var model = SaveAndRead(ctrl, session, Req("EpsilonNFA", raw));
        model.Transitions[0].Symbol.ShouldBe('\0');
    }

    [Fact]
    public void BuildViewModel_ParseSymbol_MultiCharString_UsesFirstChar()
    {
        var (ctrl, session) = CreateController();
        var model = SaveAndRead(ctrl, session, Req("DFA", "abc"));
        model.Transitions[0].Symbol.ShouldBe('a');
    }

    [Fact]
    public void BuildViewModel_PDA_StackPopSingleChar_Preserved()
    {
        var (ctrl, session) = CreateController();
        var req = new CanvasSyncRequest
        {
            Type = "PDA",
            States = [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            Transitions = [new() { FromStateId = 0, ToStateId = 1, Symbol = "a", StackPop = "Z", StackPush = "AZ" }]
        };
        var model = SaveAndRead(ctrl, session, req);
        model.Transitions[0].StackPop.ShouldBe('Z');
        model.Transitions[0].StackPush.ShouldBe("AZ");
    }

    [Fact]
    public void BuildViewModel_PDA_EpsilonStackPop_MappedToNulChar()
    {
        var (ctrl, session) = CreateController();
        var req = new CanvasSyncRequest
        {
            Type = "PDA",
            States = [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            Transitions = [new() { FromStateId = 0, ToStateId = 1, Symbol = "a", StackPop = "\\0", StackPush = "Z" }]
        };
        var model = SaveAndRead(ctrl, session, req);
        model.Transitions[0].StackPop.ShouldBe('\0');
    }

    [Fact]
    public void BuildViewModel_NonPDA_StackOperationsNull()
    {
        var (ctrl, session) = CreateController();
        var req = new CanvasSyncRequest
        {
            Type = "DFA",
            States = [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            Transitions = [new() { FromStateId = 0, ToStateId = 1, Symbol = "a" }]
        };
        var model = SaveAndRead(ctrl, session, req);
        model.Transitions[0].StackPop.ShouldBeNull();
        model.Transitions[0].StackPush.ShouldBeNull();
    }

    [Fact]
    public void BuildViewModel_PDA_EmptyStackPush_StoredAsEmpty()
    {
        var (ctrl, session) = CreateController();
        var req = new CanvasSyncRequest
        {
            Type = "PDA",
            States = [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            Transitions = [new() { FromStateId = 0, ToStateId = 1, Symbol = "a", StackPop = "Z", StackPush = "" }]
        };
        var model = SaveAndRead(ctrl, session, req);
        model.Transitions[0].StackPush.ShouldBe("");
    }

    [Fact]
    public void BuildViewModel_PDA_NullStackPush_StoredAsEmpty()
    {
        var (ctrl, session) = CreateController();
        var req = new CanvasSyncRequest
        {
            Type = "PDA",
            States = [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            Transitions = [new() { FromStateId = 0, ToStateId = 1, Symbol = "a", StackPop = "Z", StackPush = null }]
        };
        var model = SaveAndRead(ctrl, session, req);
        model.Transitions[0].StackPush.ShouldBe("");
    }

    [Theory]
    [InlineData("DFA")]
    [InlineData("NFA")]
    [InlineData("EpsilonNFA")]
    [InlineData("PDA")]
    public void BuildViewModel_IsCustomAutomaton_AlwaysTrue(string type)
    {
        var (ctrl, session) = CreateController();
        var model = SaveAndRead(ctrl, session, new CanvasSyncRequest { Type = type, States = [], Transitions = [] });
        model.IsCustomAutomaton.ShouldBeTrue();
    }

    [Fact]
    public void BuildViewModel_State_ZeroId_Allowed()
    {
        var (ctrl, session) = CreateController();
        var req = new CanvasSyncRequest
        {
            Type = "DFA",
            States = [new() { Id = 0, IsStart = true }],
            Transitions = []
        };
        var model = SaveAndRead(ctrl, session, req);
        model.States[0].Id.ShouldBe(0);
    }

    [Fact]
    public void BuildViewModel_State_LargeId_Allowed()
    {
        var (ctrl, session) = CreateController();
        var req = new CanvasSyncRequest
        {
            Type = "DFA",
            States = [new() { Id = 9999, IsStart = true }],
            Transitions = []
        };
        var model = SaveAndRead(ctrl, session, req);
        model.States[0].Id.ShouldBe(9999);
    }

    [Fact]
    public void BuildViewModel_EmptyStatesAndTransitions_EmptyLists()
    {
        var (ctrl, session) = CreateController();
        var model = SaveAndRead(ctrl, session, new CanvasSyncRequest { Type = "DFA", States = [], Transitions = [] });
        model.States.ShouldBeEmpty();
        model.Transitions.ShouldBeEmpty();
    }

    [Fact]
    public void BuildViewModel_MultipleStates_AllPreserved()
    {
        var (ctrl, session) = CreateController();
        var req = new CanvasSyncRequest
        {
            Type = "NFA",
            States = [new() { Id = 0, IsStart = true }, new() { Id = 1 }, new() { Id = 2, IsAccepting = true }],
            Transitions = []
        };
        var model = SaveAndRead(ctrl, session, req);
        model.States.Count.ShouldBe(3);
    }

    [Fact]
    public void BuildViewModel_MultipleTransitions_AllPreserved()
    {
        var (ctrl, session) = CreateController();
        var req = new CanvasSyncRequest
        {
            Type = "NFA",
            States = [new() { Id = 0, IsStart = true }, new() { Id = 1 }, new() { Id = 2 }],
            Transitions =
            [
                new() { FromStateId = 0, ToStateId = 1, Symbol = "a" },
                new() { FromStateId = 0, ToStateId = 2, Symbol = "a" },
                new() { FromStateId = 1, ToStateId = 2, Symbol = "b" }
            ]
        };
        var model = SaveAndRead(ctrl, session, req);
        model.Transitions.Count.ShouldBe(3);
    }

    // Helpers

    private static CanvasSyncRequest Req(string type, string symbol) =>
        new()
        {
            Type = type,
            States = [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            Transitions = [new() { FromStateId = 0, ToStateId = 1, Symbol = symbol }]
        };
}
