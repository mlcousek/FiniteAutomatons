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

public class CanvasApiControllerSaveTests
{
    private readonly CanvasApiController controller;
    private readonly MockSession session;

    public CanvasApiControllerSaveTests()
    {
        controller = new CanvasApiController(new NoOpLogger<CanvasApiController>());
        session = new MockSession();
        var httpContext = new DefaultHttpContext { Session = session };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    [Fact]
    public void Save_NullRequest_ReturnsBadRequest()
        => controller.Save(null).ShouldBeOfType<BadRequestObjectResult>();

    [Fact]
    public void Save_NullRequest_NothingWrittenToSession()
    {
        controller.Save(null);
        session.Keys.ShouldNotContain(CanvasApiController.SessionKey);
    }

    [Fact]
    public void Save_ValidRequest_ReturnsOk()
        => controller.Save(SimpleDfa()).ShouldBeOfType<OkObjectResult>();

    [Fact]
    public void Save_ValidRequest_WritesSessionKey()
    {
        controller.Save(SimpleDfa());
        session.Keys.ShouldContain(CanvasApiController.SessionKey);
    }

    [Fact]
    public void Save_ValidRequest_SessionContainsValidJson()
    {
        controller.Save(SimpleDfa());
        var json = ReadSessionJson();
        json.ShouldNotBeNullOrEmpty();
        Should.NotThrow(() => JsonSerializer.Deserialize<AutomatonViewModel>(json!));
    }

    [Fact]
    public void Save_ValidRequest_IsCustomAutomatonTrue()
    {
        controller.Save(SimpleDfa());
        ReadModel().IsCustomAutomaton.ShouldBeTrue();
    }

    [Theory]
    [InlineData("DFA", AutomatonType.DFA)]
    [InlineData("NFA", AutomatonType.NFA)]
    [InlineData("EpsilonNFA", AutomatonType.EpsilonNFA)]
    [InlineData("PDA", AutomatonType.PDA)]
    [InlineData("EPSILONNFA", AutomatonType.EpsilonNFA)]
    [InlineData("dfa", AutomatonType.DFA)]
    [InlineData("nfa", AutomatonType.NFA)]
    [InlineData("pda", AutomatonType.PDA)]
    [InlineData("unknown", AutomatonType.DFA)]
    [InlineData("", AutomatonType.DFA)]
    public void Save_TypeMapping_Correct(string rawType, AutomatonType expected)
    {
        controller.Save(Req(rawType, [], []));
        ReadModel().Type.ShouldBe(expected);
    }

    [Fact]
    public void Save_States_CountPreserved()
    {
        controller.Save(Req("DFA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1 }, new() { Id = 2 }], []));
        ReadModel().States.Count.ShouldBe(3);
    }

    [Fact]
    public void Save_States_IdPreserved()
    {
        controller.Save(Req("DFA", [new() { Id = 99, IsStart = true }], []));
        ReadModel().States[0].Id.ShouldBe(99);
    }

    [Fact]
    public void Save_States_IsStartPreserved()
    {
        controller.Save(Req("DFA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1, IsStart = false }], []));
        ReadModel().States.Single(s => s.Id == 0).IsStart.ShouldBeTrue();
    }

    [Fact]
    public void Save_States_IsAcceptingPreserved()
    {
        controller.Save(Req("DFA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1, IsAccepting = true }], []));
        ReadModel().States.Single(s => s.Id == 1).IsAccepting.ShouldBeTrue();
    }

    [Fact]
    public void Save_NoStates_EmptyStatesList()
    {
        controller.Save(Req("DFA", [], []));
        ReadModel().States.ShouldBeEmpty();
    }

    [Fact]
    public void Save_Transitions_CountPreserved()
    {
        controller.Save(Req("DFA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            [new() { FromStateId = 0, ToStateId = 1, Symbol = "a" }, new() { FromStateId = 1, ToStateId = 0, Symbol = "b" }]));
        ReadModel().Transitions.Count.ShouldBe(2);
    }

    [Fact]
    public void Save_Transitions_SymbolPreserved()
    {
        controller.Save(Req("DFA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            [new() { FromStateId = 0, ToStateId = 1, Symbol = "z" }]));
        ReadModel().Transitions[0].Symbol.ShouldBe('z');
    }

    [Fact]
    public void Save_Transitions_EpsilonMappedToNulChar()
    {
        controller.Save(Req("EpsilonNFA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            [new() { FromStateId = 0, ToStateId = 1, Symbol = "\\0" }]));
        ReadModel().Transitions[0].Symbol.ShouldBe('\0');
    }

    [Fact]
    public void Save_PDA_StackPopPreserved()
    {
        controller.Save(Req("PDA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            [new() { FromStateId = 0, ToStateId = 1, Symbol = "a", StackPop = "Z", StackPush = "AZ" }]));
        ReadModel().Transitions[0].StackPop.ShouldBe('Z');
    }

    [Fact]
    public void Save_PDA_StackPushPreserved()
    {
        controller.Save(Req("PDA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            [new() { FromStateId = 0, ToStateId = 1, Symbol = "a", StackPop = "Z", StackPush = "AZ" }]));
        ReadModel().Transitions[0].StackPush.ShouldBe("AZ");
    }

    [Fact]
    public void Save_PDA_EpsilonStackPopMappedToNulChar()
    {
        controller.Save(Req("PDA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            [new() { FromStateId = 0, ToStateId = 1, Symbol = "a", StackPop = "\\0", StackPush = "Z" }]));
        ReadModel().Transitions[0].StackPop.ShouldBe('\0');
    }

    [Fact]
    public void Save_NonPDA_StackOpsNull()
    {
        controller.Save(Req("DFA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            [new() { FromStateId = 0, ToStateId = 1, Symbol = "a" }]));
        var t = ReadModel().Transitions[0];
        t.StackPop.ShouldBeNull();
        t.StackPush.ShouldBeNull();
    }

    [Fact]
    public void Save_CalledTwice_SecondSaveOverwritesFirst()
    {
        controller.Save(Req("DFA", [new() { Id = 0, IsStart = true }], []));
        controller.Save(Req("NFA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1 }], []));

        ReadModel().Type.ShouldBe(AutomatonType.NFA);
        ReadModel().States.Count.ShouldBe(2);
    }

    [Fact]
    public void Clear_ReturnsOk()
        => controller.Clear().ShouldBeOfType<OkObjectResult>();

    [Fact]
    public void Clear_RemovesSessionKey()
    {
        controller.Save(SimpleDfa());
        controller.Clear();
        session.Keys.ShouldNotContain(CanvasApiController.SessionKey);
    }

    [Fact]
    public void Clear_WhenSessionEmpty_StillReturnsOk()
        => controller.Clear().ShouldBeOfType<OkObjectResult>();

    [Fact]
    public void Clear_RemovesTryGetValue()
    {
        controller.Save(SimpleDfa());
        controller.Clear();
        session.TryGetValue(CanvasApiController.SessionKey, out _).ShouldBeFalse();
    }

    private string? ReadSessionJson()
    {
        session.TryGetValue(CanvasApiController.SessionKey, out var bytes);
        return bytes.Length == 0 ? null : Encoding.UTF8.GetString(bytes);
    }
    private AutomatonViewModel ReadModel() =>
        JsonSerializer.Deserialize<AutomatonViewModel>(ReadSessionJson()!)!;

    private static CanvasSyncRequest SimpleDfa() => Req("DFA",
        [new() { Id = 0, IsStart = true }, new() { Id = 1, IsAccepting = true }],
        [new() { FromStateId = 0, ToStateId = 1, Symbol = "a" }]);

    private static CanvasSyncRequest Req(string type,
        List<CanvasSyncState> states, List<CanvasSyncTransition> transitions)
        => new() { Type = type, States = states, Transitions = transitions };
}
