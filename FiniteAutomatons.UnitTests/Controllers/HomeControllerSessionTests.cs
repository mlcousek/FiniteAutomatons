using FiniteAutomatons.Controllers;
using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using FiniteAutomatons.UnitTests.TestHelpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Shouldly;
using System.Text;
using System.Text.Json;

namespace FiniteAutomatons.UnitTests.Controllers;

public class HomeControllerSessionTests
{
    // ────────────────────────────────────────────────────────────── //
    // Session populated → Index loads from Session
    // ────────────────────────────────────────────────────────────── //

    [Fact]
    public void Index_SessionHasAutomaton_ReturnsSessionModel()
    {
        var (controller, session, _) = CreateController();
        WriteToSession(session, BuildModel(AutomatonType.NFA, stateCount: 3, isCustom: true));

        var result = controller.Index() as ViewResult;
        result.ShouldNotBeNull();
        var model = result.Model as AutomatonViewModel;
        model.ShouldNotBeNull();
        model.Type.ShouldBe(AutomatonType.NFA);
        model.States.Count.ShouldBe(3);
    }

    [Fact]
    public void Index_EmptySession_ReturnsDefaultAutomaton()
    {
        var (controller, _, _) = CreateController();
        var result = controller.Index() as ViewResult;
        var model = result?.Model as AutomatonViewModel;
        model.ShouldNotBeNull();
        model.IsCustomAutomaton.ShouldBeFalse();
    }

    [Fact]
    public void Index_CorruptedSessionJson_FallsBackToDefault()
    {
        var (controller, session, _) = CreateController();
        session.Set(CanvasApiController.SessionKey, Encoding.UTF8.GetBytes("{ not valid json }"));

        var result = controller.Index() as ViewResult;
        var model = result?.Model as AutomatonViewModel;
        model.ShouldNotBeNull();
        model.IsCustomAutomaton.ShouldBeFalse();
    }

    [Fact]
    public void Index_NullSessionValue_FallsBackToDefault()
    {
        var (controller, _, _) = CreateController();
        var result = controller.Index() as ViewResult;
        var model = result?.Model as AutomatonViewModel;
        model.ShouldNotBeNull();
        model.IsCustomAutomaton.ShouldBeFalse();
    }

    // ────────────────────────────────────────────────────────────── //
    // TempData priority over Session
    // ────────────────────────────────────────────────────────────── //

    [Fact]
    public void Index_BothTempDataAndSession_TempDataWins()
    {
        var (controller, session, tempDataService) = CreateController();

        WriteToSession(session, BuildModel(AutomatonType.NFA, 2, true));
        tempDataService.SetupReturn(BuildModel(AutomatonType.PDA, 4, true));

        var result = controller.Index() as ViewResult;
        var model = (result!.Model as AutomatonViewModel)!;
        model.Type.ShouldBe(AutomatonType.PDA);
    }

    [Fact]
    public void Index_TempDataHasAutomaton_SessionEmpty_TempDataUsed()
    {
        var (controller, _, tempDataService) = CreateController();
        tempDataService.SetupReturn(BuildModel(AutomatonType.NFA, 5, true));

        var result = controller.Index() as ViewResult;
        ((result!.Model as AutomatonViewModel)!).Type.ShouldBe(AutomatonType.NFA);
    }

    // ────────────────────────────────────────────────────────────── //
    // Session automaton properties preserved
    // ────────────────────────────────────────────────────────────── //

    [Fact]
    public void Index_SessionAutomaton_StatesCountPreserved()
    {
        var (controller, session, _) = CreateController();
        WriteToSession(session, BuildModel(AutomatonType.DFA, stateCount: 7, isCustom: true));

        var model = ((controller.Index() as ViewResult)!.Model as AutomatonViewModel)!;
        model.States.Count.ShouldBe(7);
    }

    [Fact]
    public void Index_SessionAutomaton_TransitionsPreserved()
    {
        var (controller, session, _) = CreateController();
        var automaton = BuildModel(AutomatonType.DFA, stateCount: 2, isCustom: true);
        automaton.Transitions.Add(new() { FromStateId = 0, ToStateId = 1, Symbol = 'x' });
        WriteToSession(session, automaton);

        var model = ((controller.Index() as ViewResult)!.Model as AutomatonViewModel)!;
        model.Transitions.Count.ShouldBe(1);
        model.Transitions[0].Symbol.ShouldBe('x');
    }

    [Fact]
    public void Index_SessionAutomaton_PDAType_Preserved()
    {
        var (controller, session, _) = CreateController();
        WriteToSession(session, BuildModel(AutomatonType.PDA, 3, true));

        var model = ((controller.Index() as ViewResult)!.Model as AutomatonViewModel)!;
        model.Type.ShouldBe(AutomatonType.PDA);
    }

    [Fact]
    public void Index_SessionAutomaton_EpsilonNFA_Preserved()
    {
        var (controller, session, _) = CreateController();
        WriteToSession(session, BuildModel(AutomatonType.EpsilonNFA, 2, true));

        var model = ((controller.Index() as ViewResult)!.Model as AutomatonViewModel)!;
        model.Type.ShouldBe(AutomatonType.EpsilonNFA);
    }

    // ────────────────────────────────────────────────────────────── //
    // Session with start/accepting states preserved
    // ────────────────────────────────────────────────────────────── //

    [Fact]
    public void Index_SessionAutomaton_StartStatePreserved()
    {
        var (controller, session, _) = CreateController();
        var m = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            IsCustomAutomaton = true,
            States = [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            Transitions = []
        };
        WriteToSession(session, m);

        var model = ((controller.Index() as ViewResult)!.Model as AutomatonViewModel)!;
        model.States.Single(s => s.Id == 0).IsStart.ShouldBeTrue();
    }

    [Fact]
    public void Index_SessionAutomaton_AcceptingStatePreserved()
    {
        var (controller, session, _) = CreateController();
        var m = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            IsCustomAutomaton = true,
            States = [new() { Id = 0, IsStart = true }, new() { Id = 1, IsAccepting = true }],
            Transitions = []
        };
        WriteToSession(session, m);

        var model = ((controller.Index() as ViewResult)!.Model as AutomatonViewModel)!;
        model.States.Single(s => s.Id == 1).IsAccepting.ShouldBeTrue();
    }

    // ────────────────────────────────────────────────────────────── //
    // Default automaton has data
    // ────────────────────────────────────────────────────────────── //

    [Fact]
    public void Index_NoSession_DefaultAutomatonHasStates()
    {
        var (controller, _, _) = CreateController();
        var model = ((controller.Index() as ViewResult)!.Model as AutomatonViewModel)!;
        model.States.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Index_NoSession_DefaultAutomatonHasStartState()
    {
        var (controller, _, _) = CreateController();
        var model = ((controller.Index() as ViewResult)!.Model as AutomatonViewModel)!;
        model.States.Any(s => s.IsStart).ShouldBeTrue();
    }

    [Fact]
    public void Index_NoSession_DefaultAutomatonHasAlphabet()
    {
        var (controller, _, _) = CreateController();
        var model = ((controller.Index() as ViewResult)!.Model as AutomatonViewModel)!;
        model.Alphabet.ShouldNotBeEmpty();
    }

    // ────────────────────────────────────────────────────────────── //
    // Helpers
    // ────────────────────────────────────────────────────────────── //

    private static (HomeController ctrl, MockSession session, ConfigurableMockTempDataService tempData) CreateController()
    {
        var session = new MockSession();
        var tempDataService = new ConfigurableMockTempDataService();
        var homeService = new MockHomeAutomatonService();
        var minimizationService = new MockAutomatonMinimizationService();
        var logger = new NoOpLogger<HomeController>();

        var controller = new HomeController(logger, tempDataService, homeService, minimizationService);

        var httpContext = new DefaultHttpContext { Session = session };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new TestTempDataProvider());

        return (controller, session, tempDataService);
    }

    private static AutomatonViewModel BuildModel(AutomatonType type, int stateCount, bool isCustom)
        => new()
        {
            Type = type,
            IsCustomAutomaton = isCustom,
            States = [.. Enumerable.Range(0, stateCount).Select(i => new State { Id = i, IsStart = i == 0 })],
            Transitions = []
        };

    private static void WriteToSession(MockSession session, AutomatonViewModel model)
    {
        var json = JsonSerializer.Serialize(model);
        session.Set(CanvasApiController.SessionKey, Encoding.UTF8.GetBytes(json));
    }
}

public class ConfigurableMockTempDataService : IAutomatonTempDataService
{
    private AutomatonViewModel? model;
    public void SetupReturn(AutomatonViewModel model) => this.model = model;

    public (bool Success, AutomatonViewModel? Model) TryGetCustomAutomaton(ITempDataDictionary tempData)
        => (model != null, model);

    public void StoreCustomAutomaton(ITempDataDictionary tempData, AutomatonViewModel model) { }
    public void StoreErrorMessage(ITempDataDictionary tempData, string message) { }
    public void StoreConversionMessage(ITempDataDictionary tempData, string message) { }

    public (bool Success, AutomatonViewModel? Model) TryGetSessionAutomaton(ISession session, string sessionKey)
    {
        var json = session.GetString(sessionKey);
        if (string.IsNullOrEmpty(json)) return (false, null);

        try
        {
            var sessionModel = JsonSerializer.Deserialize<AutomatonViewModel>(json);
            if (sessionModel != null)
            {
                sessionModel.IsCustomAutomaton = true;
                sessionModel.States ??= [];
                sessionModel.Transitions ??= [];
            }
            return (sessionModel != null, sessionModel);
        }
        catch
        {
            return (false, null);
        }
    }
}
