using System.Collections.Generic;
using System.Linq;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Services.Interfaces;
using FiniteAutomatons.Services.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FiniteAutomatons.UnitTests.Services;

public class AutomatonPresetPdaTests
{
    private readonly MockGenerator mockGenerator = new();
    private readonly MockMinimization mockMin = new();
    private readonly AutomatonPresetService service;

    public AutomatonPresetPdaTests()
    {
        service = new AutomatonPresetService(mockGenerator, mockMin, NullLogger<AutomatonPresetService>.Instance);
    }

    // Local simple mocks for generator and minimization
    private class MockGenerator : IAutomatonGeneratorService
    {
        public AutomatonViewModel? RandomAutomatonToReturn { get; set; }

        public AutomatonViewModel GenerateRandomAutomaton(AutomatonType type, int stateCount, int transitionCount, int alphabetSize = 3, double acceptingStateRatio = 0.3, int? seed = null)
        {
            if (RandomAutomatonToReturn != null) return RandomAutomatonToReturn;
            stateCount = Math.Max(1, stateCount);
            var states = new List<State>();
            for (int i = 1; i <= stateCount; i++) states.Add(new State { Id = i, IsStart = i == 1, IsAccepting = i == stateCount });

            var transitions = new List<Transition>();
            var alph = Math.Max(1, alphabetSize);
            for (int i = 0; i < alph; i++)
            {
                var sym = (char)('a' + i);
                transitions.Add(new Transition { FromStateId = 1, ToStateId = Math.Min(2, stateCount), Symbol = sym });
            }

            return new AutomatonViewModel { Type = type, States = states, Transitions = transitions, IsCustomAutomaton = true };
        }

        public bool ValidateGenerationParameters(AutomatonType type, int stateCount, int transitionCount, int alphabetSize) => true;
    }

    private class MockMinimization : IAutomatonMinimizationService
    {
        public (AutomatonViewModel Result, string Message) MinimizeDfa(AutomatonViewModel model)
        {
            return (model, "ok");
        }

        public MinimizationAnalysis AnalyzeAutomaton(AutomatonViewModel model)
        {
            return new MinimizationAnalysis(true, true, model.States.Count, model.States.Count, model.States.Count);
        }
    }

    [Fact]
    public void GenerateRandomPda_ReturnsPdaModel()
    {
        var result = service.GenerateRandomPda(4, 8, 3, 0.4, 42);
        result.ShouldNotBeNull();
        result.Type.ShouldBe(AutomatonType.PDA);
        result.States.ShouldNotBeEmpty();
    }

    [Fact]
    public void GeneratePdaWithPushPopPairs_IncludesStackTransitions()
    {
        var result = service.GeneratePdaWithPushPopPairs(5, 12, 3, 0.3, 7);
        result.ShouldNotBeNull();
        result.Type.ShouldBe(AutomatonType.PDA);
        result.Transitions.ShouldNotBeNull();
        // At least one transition should have either StackPush or StackPop set
        result.Transitions.Any(t => !string.IsNullOrEmpty(t.StackPush) || t.StackPop.HasValue).ShouldBeTrue();
    }

    [Fact]
    public void GenerateBalancedParenthesesPda_HasPushAndPopTransitions()
    {
        var result = service.GenerateBalancedParenthesesPda(4);
        result.ShouldNotBeNull();
        result.Type.ShouldBe(AutomatonType.PDA);
        // Expect a push on '(' and a pop on ')'
        result.Transitions.Any(t => t.Symbol == '(' && !string.IsNullOrEmpty(t.StackPush)).ShouldBeTrue();
        result.Transitions.Any(t => t.Symbol == ')' && t.StackPop.HasValue).ShouldBeTrue();
    }
}
