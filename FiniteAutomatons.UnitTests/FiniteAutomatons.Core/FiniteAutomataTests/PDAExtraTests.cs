using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FiniteAutomatons.UnitTests.FiniteAutomatons.Core.FiniteAutomataTests;

public class PDAExtraTests
{
    [Fact]
    public void StepBackward_RestoresExactStackSequence()
    {
        var pda = new PDA();
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" });

        var state = pda.StartExecution("aa");
        var pdaState = state as PDAExecutionState;
        pdaState.ShouldNotBeNull();

        // initial: only bottom
        var before0 = pdaState.Stack.ToArray();
        before0.Length.ShouldBeGreaterThan(0);

        pda.StepForward(state); // push X
        var after1 = pdaState.Stack.ToArray(); // top-first
        // expect: top 'X', then bottom
        after1[0].ShouldBe('X');

        pda.StepForward(state); // push X again
        var after2 = pdaState.Stack.ToArray(); // top-first
        after2[0].ShouldBe('X');
        after2[1].ShouldBe('X');

        // step backward -> should restore to after1 sequence
        pda.StepBackward(state);
        var restored = pdaState.Stack.ToArray();
        restored.Length.ShouldBe(after1.Length);
        for (int i = 0; i < restored.Length; i++) restored[i].ShouldBe(after1[i]);
    }

    [Fact]
    public void PDAValidator_Considers_NullAndEpsilonStackPop_Equivalent()
    {
        var validator = new AutomatonValidationService(new NullLogger<AutomatonValidationService>());
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.PDA,
            States = new List<State> { new() { Id = 1, IsStart = true, IsAccepting = false } },
            Transitions = new List<Transition>
            {
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = null },
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0' }
            }
        };

        var (isValid, errors) = validator.ValidateAutomaton(model);
        isValid.ShouldBeFalse();
        errors.Any(e => e.Contains("PDA must be deterministic")).ShouldBeTrue();
    }
}
