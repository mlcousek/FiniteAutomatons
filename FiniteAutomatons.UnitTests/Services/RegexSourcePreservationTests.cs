using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Services;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace FiniteAutomatons.UnitTests.Services;

public class RegexSourcePreservationTests
{
    private readonly RegexToAutomatonService regexService;
    private readonly AutomatonConversionService conversionService;
    private readonly AutomatonMinimizationService minimizationService;

    // lightweight null logger used for tests
    internal class NullLogger2<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    public RegexSourcePreservationTests()
    {
        regexService = new RegexToAutomatonService(new NullLogger2<RegexToAutomatonService>());
        // instantiate builder and analysis with simple null loggers
        var builder = new AutomatonBuilderService(new NullLogger2<AutomatonBuilderService>());
        var analysis = new AutomatonAnalysisService();
        conversionService = new AutomatonConversionService(builder, new NullLogger2<AutomatonConversionService>());
        minimizationService = new AutomatonMinimizationService(builder, analysis, new NullLogger2<AutomatonMinimizationService>());
    }

    [Fact]
    public void SourceRegex_IsPreserved_WhenConvertedToNfa()
    {
        var regex = "(a|b)*c";
        var enfa = regexService.BuildEpsilonNfaFromRegex(regex);

        var model = new AutomatonViewModel
        {
            Type = AutomatonType.EpsilonNFA,
            States = [.. enfa.States.Select(s => new State { Id = s.Id, IsStart = s.IsStart, IsAccepting = s.IsAccepting })],
            Transitions = [.. enfa.Transitions.Select(t => new Transition { FromStateId = t.FromStateId, ToStateId = t.ToStateId, Symbol = t.Symbol })],
            IsCustomAutomaton = true,
            SourceRegex = regex
        };

        var (converted, warnings) = conversionService.ConvertAutomatonType(model, AutomatonType.NFA);

        converted.SourceRegex.ShouldBe(regex);
    }

    [Fact]
    public void SourceRegex_IsPreserved_WhenConvertedToDfa()
    {
        var regex = "a+b?";
        var enfa = regexService.BuildEpsilonNfaFromRegex(regex);

        var model = new AutomatonViewModel
        {
            Type = AutomatonType.EpsilonNFA,
            States = [.. enfa.States.Select(s => new State { Id = s.Id, IsStart = s.IsStart, IsAccepting = s.IsAccepting })],
            Transitions = [.. enfa.Transitions.Select(t => new Transition { FromStateId = t.FromStateId, ToStateId = t.ToStateId, Symbol = t.Symbol })],
            IsCustomAutomaton = true,
            SourceRegex = regex
        };

        var dfaModel = conversionService.ConvertToDFA(model);

        dfaModel.Type.ShouldBe(AutomatonType.DFA);
        dfaModel.SourceRegex.ShouldBe(regex);
    }

    [Fact]
    public void SourceRegex_IsPreserved_AfterMinimization()
    {
        var regex = "(0|1)*01";
        var enfa = regexService.BuildEpsilonNfaFromRegex(regex);

        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            // convert enfa -> DFA first for minimization: use conversion service
            IsCustomAutomaton = true,
            SourceRegex = regex
        };

        // Build DFA from ENFA using conversion service
        var enfaModel = new AutomatonViewModel
        {
            Type = AutomatonType.EpsilonNFA,
            States = [.. enfa.States.Select(s => new State { Id = s.Id, IsStart = s.IsStart, IsAccepting = s.IsAccepting })],
            Transitions = [.. enfa.Transitions.Select(t => new Transition { FromStateId = t.FromStateId, ToStateId = t.ToStateId, Symbol = t.Symbol })],
            IsCustomAutomaton = true,
            SourceRegex = regex
        };

        var dfa = conversionService.ConvertToDFA(enfaModel);
        dfa.SourceRegex.ShouldBe(regex);

        var (minimized, msg) = minimizationService.MinimizeDfa(dfa);
        minimized.SourceRegex.ShouldBe(regex);
    }
}
