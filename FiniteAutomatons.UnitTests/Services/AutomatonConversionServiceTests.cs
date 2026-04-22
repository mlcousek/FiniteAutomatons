using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using FiniteAutomatons.Services.Services;
using Shouldly;

namespace FiniteAutomatons.UnitTests.Services;

public class AutomatonConversionServiceTests
{
    private readonly AutomatonConversionService service;

    public AutomatonConversionServiceTests()
    {
        var mockBuilderService = new MockAutomatonBuilderService();
        var logger = new TestLogger<AutomatonConversionService>();
        service = new AutomatonConversionService(mockBuilderService, logger);
    }

    [Fact]
    public void ConvertAutomatonType_DFAToNFA_ShouldSucceed()
    {
        // Arrange
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }
            ]
        };

        // Act
        var (convertedModel, warnings) = service.ConvertAutomatonType(model, AutomatonType.NFA);

        // Assert
        convertedModel.Type.ShouldBe(AutomatonType.NFA);
        convertedModel.States.Count.ShouldBe(2);
        convertedModel.Transitions.Count.ShouldBe(1);
        warnings.ShouldBeEmpty();
    }

    [Fact]
    public void ConvertAutomatonType_EpsilonNFAToNFA_ShouldRemoveEpsilonTransitions()
    {
        // Arrange
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.EpsilonNFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 1, ToStateId = 2, Symbol = '\0' } // Epsilon transition
            ]
        };

        // Act
        var (convertedModel, warnings) = service.ConvertAutomatonType(model, AutomatonType.NFA);

        // Assert
        convertedModel.Type.ShouldBe(AutomatonType.NFA);
        convertedModel.Transitions.Count.ShouldBe(1);
        convertedModel.Transitions.ShouldNotContain(t => t.Symbol == '\0');
        // Updated expected warning string after refactor
        warnings.ShouldContain("Converted EpsilonNFA to NFA via epsilon-closure elimination. Epsilon transitions removed.");
        // Optional: start state becomes accepting due to closure
        convertedModel.States.First(s => s.IsStart).IsAccepting.ShouldBeTrue();
    }

    [Fact]
    public void ConvertToDFA_AlreadyDFA_ShouldReturnOriginal()
    {
        // Arrange
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = true }
            ]
        };

        // Act
        var result = service.ConvertToDFA(model);

        // Assert
        result.ShouldBe(model);
    }

    [Fact]
    public void ConvertToDFA_EpsilonNfaDirect_ShouldBehaveLikeExpectedLanguage()
    {
        // Arrange
        var model = CreateImageScenarioEpsilonNfa();

        // Act
        var dfaModel = service.ConvertToDFA(model);
        var dfa = new MockAutomatonBuilderService().CreateDFA(dfaModel);

        // Assert
        dfa.Execute("").ShouldBeFalse();
        dfa.Execute("a").ShouldBeTrue();
        dfa.Execute("u").ShouldBeTrue();
        dfa.Execute("aeiou").ShouldBeTrue();
        dfa.Execute("b").ShouldBeFalse();
        dfa.Execute("ab").ShouldBeFalse();
    }

    [Fact]
    public void ConvertAutomatonType_EpsilonNfaToDfa_ShouldMatchTwoStepConversion()
    {
        // Arrange
        var model = CreateImageScenarioEpsilonNfa();

        // Act
        var (directDfaModel, directWarnings) = service.ConvertAutomatonType(model, AutomatonType.DFA);
        var (intermediateNfaModel, _) = service.ConvertAutomatonType(model, AutomatonType.NFA);
        var twoStepDfaModel = service.ConvertToDFA(intermediateNfaModel);

        // Assert
        directDfaModel.Type.ShouldBe(AutomatonType.DFA);
        directWarnings.ShouldContain("Converted EpsilonNFA to DFA via intermediate NFA conversion and determinization.");

        directDfaModel.States.Count.ShouldBe(twoStepDfaModel.States.Count);
        directDfaModel.Transitions.Count.ShouldBe(twoStepDfaModel.Transitions.Count);

        var directTransitions = directDfaModel.Transitions
            .Select(t => $"{t.FromStateId}-{t.Symbol}-{t.ToStateId}")
            .OrderBy(x => x)
            .ToArray();
        var twoStepTransitions = twoStepDfaModel.Transitions
            .Select(t => $"{t.FromStateId}-{t.Symbol}-{t.ToStateId}")
            .OrderBy(x => x)
            .ToArray();

        directTransitions.ShouldBe(twoStepTransitions);
    }

    private static AutomatonViewModel CreateImageScenarioEpsilonNfa()
    {
        return new AutomatonViewModel
        {
            Type = AutomatonType.EpsilonNFA,
            States =
            [
                new() { Id = 1, IsStart = false, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = false },
                new() { Id = 3, IsStart = true, IsAccepting = false },
                new() { Id = 4, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 3, ToStateId = 1, Symbol = '\0' },
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'e' },
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'i' },
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'o' },
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'u' },
                new() { FromStateId = 2, ToStateId = 1, Symbol = '\0' },
                new() { FromStateId = 2, ToStateId = 4, Symbol = '\0' }
            ]
        };
    }
}

public class MockAutomatonBuilderService : IAutomatonBuilderService
{
    public Automaton CreateAutomatonFromModel(AutomatonViewModel model)
    {
        // Respect the model.Type to create appropriate automaton instance
        return model.Type switch
        {
            AutomatonType.EpsilonNFA => CreateEpsilonNFA(model),
            AutomatonType.NFA => CreateNFA(model),
            AutomatonType.DFA => CreateDFA(model),
            AutomatonType.DPDA => CreateDPDA(model),
            _ => CreateDFA(model)
        };
    }

    public DFA CreateDFA(AutomatonViewModel model)
    {
        var dfa = new DFA();
        foreach (var state in model.States ?? []) dfa.States.Add(state);
        foreach (var transition in model.Transitions ?? []) dfa.Transitions.Add(transition);
        var start = model.States?.FirstOrDefault(s => s.IsStart);
        if (start != null) dfa.SetStartState(start.Id);
        return dfa;
    }

    public NFA CreateNFA(AutomatonViewModel model)
    {
        var nfa = new NFA();
        foreach (var state in model.States ?? []) nfa.States.Add(state);
        foreach (var transition in model.Transitions ?? []) nfa.Transitions.Add(transition);
        var start = model.States?.FirstOrDefault(s => s.IsStart);
        if (start != null) nfa.SetStartState(start.Id);
        return nfa;
    }

    public EpsilonNFA CreateEpsilonNFA(AutomatonViewModel model)
    {
        var enfa = new EpsilonNFA();
        foreach (var state in model.States ?? []) enfa.States.Add(state);
        foreach (var transition in model.Transitions ?? []) enfa.Transitions.Add(transition);
        var start = model.States?.FirstOrDefault(s => s.IsStart);
        if (start != null) enfa.SetStartState(start.Id);
        return enfa;
    }

    public DPDA CreateDPDA(AutomatonViewModel model)
    {
        var pda = new DPDA();
        foreach (var state in model.States ?? []) pda.States.Add(state);
        foreach (var transition in model.Transitions ?? []) pda.Transitions.Add(transition);
        var start = model.States?.FirstOrDefault(s => s.IsStart);
        if (start != null) pda.SetStartState(start.Id);
        return pda;
    }

    public NPDA CreateNPDA(AutomatonViewModel model)
    {
        var npda = new NPDA();
        foreach (var state in model.States ?? []) npda.States.Add(state);
        foreach (var transition in model.Transitions ?? []) npda.Transitions.Add(transition);
        var start = model.States?.FirstOrDefault(s => s.IsStart);
        if (start != null) npda.SetStartState(start.Id);
        return npda;
    }
}

