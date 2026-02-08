using FiniteAutomatons.Core.Configuration;
using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FiniteAutomatons.UnitTests.Services;

public class PDAValidationAndSafetyTests
{
    [Fact]
    public void Validator_Detects_PDA_Determinism_Conflicts()
    {
        var validator = new AutomatonValidationService(NullLogger<AutomatonValidationService>.Instance);
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.PDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = 'X' },
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a', StackPop = 'X' }
            ]
        };

        var (isValid, errors) = validator.ValidateAutomaton(model);
        isValid.ShouldBeFalse();
        errors.ShouldContain(e => e.Contains("PDA must be deterministic"));
    }

    [Fact]
    public void PDA_EpsilonPushLoop_IsBounded_By_Settings()
    {
        // temporarily lower safety settings for test
        var oldMaxEps = PdaExecutionSettings.MaxEpsilonIterations;
        var oldStackTol = PdaExecutionSettings.MaxStackGrowthTolerance;
        try
        {
            PdaExecutionSettings.MaxEpsilonIterations = 50;
            PdaExecutionSettings.MaxStackGrowthTolerance = 200;

            var pda = new PDA();
            pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
            pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = '\0', StackPop = '\0', StackPush = "A" });

            var state = (PDAExecutionState)pda.StartExecution("");
            pda.ExecuteAll(state);
            // stack should be bounded by the tolerance
            state.Stack.Count.ShouldBeLessThanOrEqualTo(PdaExecutionSettings.MaxStackGrowthTolerance);
        }
        finally
        {
            PdaExecutionSettings.MaxEpsilonIterations = oldMaxEps;
            PdaExecutionSettings.MaxStackGrowthTolerance = oldStackTol;
        }
    }
}
