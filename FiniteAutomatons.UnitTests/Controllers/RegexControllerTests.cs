using FiniteAutomatons.Controllers;
using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FiniteAutomatons.UnitTests.Controllers;

public class RegexControllerTests
{
    private class MockRegexToAutomatonService : IRegexToAutomatonService
    {
        public EpsilonNFA? ResultToReturn { get; set; }
        public Exception? ExceptionToThrow { get; set; }
        public string? LastRegexReceived { get; set; }

        public EpsilonNFA BuildEpsilonNfaFromRegex(string regex)
        {
            LastRegexReceived = regex;

            if (ExceptionToThrow != null)
                throw ExceptionToThrow;

            return ResultToReturn ?? CreateDefaultEnfa();
        }

        private static EpsilonNFA CreateDefaultEnfa()
        {
            var enfa = new EpsilonNFA();
            enfa.AddState(new State { Id = 1, IsStart = true });
            enfa.AddState(new State { Id = 2, IsAccepting = true });
            enfa.AddTransition(1, 2, 'a');
            return enfa;
        }
    }

    private class MockRegexPresetService : IRegexPresetService
    {
        private readonly List<RegexPreset> presets = [];

        public void AddPreset(RegexPreset preset)
        {
            presets.Add(preset);
        }

        public IEnumerable<RegexPreset> GetAllPresets() => presets;

        public RegexPreset? GetPresetByKey(string key) =>
            presets.FirstOrDefault(p => p.Key == key);
    }

    private class MockAutomatonTempDataService : IAutomatonTempDataService
    {
        public AutomatonViewModel? StoredAutomaton { get; private set; }
        public string? StoredMessage { get; private set; }
        public int StoreAutomatonCallCount { get; private set; }
        public int StoreMessageCallCount { get; private set; }

        public (bool Success, AutomatonViewModel? Model) TryGetCustomAutomaton(ITempDataDictionary tempData)
        {
            return (StoredAutomaton != null, StoredAutomaton);
        }

        public void StoreCustomAutomaton(ITempDataDictionary tempData, AutomatonViewModel model)
        {
            StoredAutomaton = model;
            StoreAutomatonCallCount++;
        }

        public void StoreConversionMessage(ITempDataDictionary tempData, string message)
        {
            StoredMessage = message;
            StoreMessageCallCount++;
        }

        public void StoreErrorMessage(ITempDataDictionary tempData, string message) { }
    }

    private readonly MockRegexToAutomatonService mockRegexService;
    private readonly MockRegexPresetService mockPresetService;
    private readonly MockAutomatonTempDataService mockTempDataService;

    public RegexControllerTests()
    {
        mockRegexService = new MockRegexToAutomatonService();
        mockPresetService = new MockRegexPresetService();
        mockTempDataService = new MockAutomatonTempDataService();
    }

    private RegexController CreateController()
    {
        var logger = NullLogger<RegexController>.Instance;
        var controller = new RegexController(
            logger,
            mockTempDataService,
            mockRegexService,
            mockPresetService);

        var httpContext = new DefaultHttpContext();
        var tempData = new TempDataDictionary(httpContext, new TestTempDataProvider());
        controller.TempData = tempData;

        var actionContext = new ActionContext(httpContext, new Microsoft.AspNetCore.Routing.RouteData(), new ControllerActionDescriptor());
        controller.ControllerContext = new ControllerContext(actionContext);

        var urlHelper = new MockUrlHelper();
        controller.Url = urlHelper;

        return controller;
    }

    private class MockUrlHelper : IUrlHelper
    {
        public ActionContext ActionContext => new();
        public string? Action(UrlActionContext actionContext) => "/";
        public string? Content(string? contentPath) => contentPath;
        public bool IsLocalUrl(string? url) => true;
        public string? Link(string? routeName, object? values) => "/";
        public string? RouteUrl(UrlRouteContext routeContext) => "/";
    }

    [Fact]
    public void BuildFromRegex_EmptyRegex_ReturnsErrorJson()
    {
        var controller = CreateController();

        var result = controller.BuildFromRegex("") as JsonResult;

        result.ShouldNotBeNull();
        var value = result.Value;
        value.ShouldNotBeNull();
        var successProp = value.GetType().GetProperty("success");
        successProp.ShouldNotBeNull();
        var success = successProp.GetValue(value);
        success.ShouldBe(false);
    }

    [Fact]
    public void BuildFromRegex_WhitespaceRegex_ReturnsErrorJson()
    {
        var controller = CreateController();

        var result = controller.BuildFromRegex("   ") as JsonResult;

        result.ShouldNotBeNull();
        var value = result.Value;
        value.ShouldNotBeNull();
        var successProp = value.GetType().GetProperty("success");
        successProp.ShouldNotBeNull();
        var success = successProp.GetValue(value);
        success.ShouldBe(false);
    }

    [Fact]
    public void BuildFromRegex_ValidPattern_ReturnsSuccess()
    {
        var controller = CreateController();

        var result = controller.BuildFromRegex("a") as JsonResult;

        result.ShouldNotBeNull();
        var value = result.Value;
        value.ShouldNotBeNull();
        var successProp = value.GetType().GetProperty("success");
        successProp.ShouldNotBeNull();
        var success = successProp.GetValue(value);
        success.ShouldBe(true);

        mockTempDataService.StoreAutomatonCallCount.ShouldBe(1);
        mockTempDataService.StoreMessageCallCount.ShouldBe(1);
        mockTempDataService.StoredAutomaton.ShouldNotBeNull();
        mockTempDataService.StoredAutomaton.SourceRegex.ShouldBe("a");
    }

    [Fact]
    public void BuildFromRegex_StoresSourceRegex()
    {
        var controller = CreateController();

        controller.BuildFromRegex("(a|b)*c");

        mockTempDataService.StoredAutomaton.ShouldNotBeNull();
        mockTempDataService.StoredAutomaton.SourceRegex.ShouldBe("(a|b)*c");
        mockTempDataService.StoredAutomaton.Type.ShouldBe(AutomatonType.EpsilonNFA);
    }

    [Fact]
    public void BuildFromRegex_ServiceThrowsUnsupportedFeature_ReturnsDetailedError()
    {
        var controller = CreateController();
        mockRegexService.ExceptionToThrow = new ArgumentException("Negated character classes are not supported");

        var result = controller.BuildFromRegex("[^abc]") as JsonResult;

        result.ShouldNotBeNull();
        var value = result.Value;
        value.ShouldNotBeNull();
        var successProp = value.GetType().GetProperty("success");
        var errorProp = value.GetType().GetProperty("error");
        successProp.ShouldNotBeNull();
        errorProp.ShouldNotBeNull();

        var success = successProp.GetValue(value);
        var error = errorProp.GetValue(value) as string;

        success.ShouldBe(false);
        error.ShouldNotBeNull();
        error.ShouldContain("not supported");
        error.ShouldContain("Supported:");
    }

    [Fact]
    public void BuildFromRegex_ServiceThrowsGenericException_ReturnsError()
    {
        var controller = CreateController();
        mockRegexService.ExceptionToThrow = new InvalidOperationException("Something went wrong");

        var result = controller.BuildFromRegex("a*") as JsonResult;

        result.ShouldNotBeNull();
        var value = result.Value;
        value.ShouldNotBeNull();
        var successProp = value.GetType().GetProperty("success");
        successProp.ShouldNotBeNull();
        var success = successProp.GetValue(value);
        success.ShouldBe(false);
    }

    [Fact]
    public void BuildFromPreset_EmptyKey_ReturnsError()
    {
        var controller = CreateController();

        var result = controller.BuildFromPreset("") as JsonResult;

        result.ShouldNotBeNull();
        var value = result.Value;
        value.ShouldNotBeNull();
        var successProp = value.GetType().GetProperty("success");
        successProp.ShouldNotBeNull();
        var success = successProp.GetValue(value);
        success.ShouldBe(false);
    }

    [Fact]
    public void BuildFromPreset_InvalidKey_ReturnsError()
    {
        var controller = CreateController();

        var result = controller.BuildFromPreset("invalid") as JsonResult;

        result.ShouldNotBeNull();
        var value = result.Value;
        value.ShouldNotBeNull();
        var successProp = value.GetType().GetProperty("success");
        successProp.ShouldNotBeNull();
        var success = successProp.GetValue(value);
        success.ShouldBe(false);
    }

    [Fact]
    public void BuildFromPreset_ValidKey_CallsBuildFromRegex()
    {
        var controller = CreateController();
        var preset = new RegexPreset(
            "test",
            "Test Pattern",
            "a*",
            "Test description",
            ["a"],
            ["b"]);

        mockPresetService.AddPreset(preset);

        var result = controller.BuildFromPreset("test") as JsonResult;

        result.ShouldNotBeNull();
        mockRegexService.LastRegexReceived.ShouldBe("a*");
    }

    [Fact]
    public void GetPresets_ReturnsJsonWithAllPresets()
    {
        var controller = CreateController();
        mockPresetService.AddPreset(new("key1", "Name1", "a", "Desc1", ["a"], ["b"]));
        mockPresetService.AddPreset(new("key2", "Name2", "b*", "Desc2", ["b"], ["a"]));

        var result = controller.GetPresets() as JsonResult;

        result.ShouldNotBeNull();
        result.Value.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new RegexController(null!, mockTempDataService, mockRegexService, mockPresetService));
    }

    [Fact]
    public void Constructor_NullTempDataService_ThrowsArgumentNullException()
    {
        var logger = NullLogger<RegexController>.Instance;
        Should.Throw<ArgumentNullException>(() =>
            new RegexController(logger, null!, mockRegexService, mockPresetService));
    }

    [Fact]
    public void Constructor_NullRegexService_ThrowsArgumentNullException()
    {
        var logger = NullLogger<RegexController>.Instance;
        Should.Throw<ArgumentNullException>(() =>
            new RegexController(logger, mockTempDataService, null!, mockPresetService));
    }

    [Fact]
    public void Constructor_NullPresetService_ThrowsArgumentNullException()
    {
        var logger = NullLogger<RegexController>.Instance;
        Should.Throw<ArgumentNullException>(() =>
            new RegexController(logger, mockTempDataService, mockRegexService, null!));
    }
}
