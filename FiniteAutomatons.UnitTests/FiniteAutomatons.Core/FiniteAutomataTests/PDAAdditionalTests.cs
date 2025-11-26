using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using System.Text;

namespace FiniteAutomatons.UnitTests.FiniteAutomatons.Core.FiniteAutomataTests;

public class PDAAdditionalTests
{
    [Fact]
    public void AutomatonExecutionService_PDA_StateRoundTrip_SerializesStackAndHistory()
    {
        var builder = new AutomatonBuilderService(new NullLogger<AutomatonBuilderService>());
        var execSvc = new AutomatonExecutionService(builder, new NullLogger<AutomatonExecutionService>());

        // Build PDA model and runtime PDA
        var pda = new PDA();
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = true });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = 'X', StackPush = null });

        // Build matching view model
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.PDA,
            States = [.. pda.States.Select(s => new State { Id = s.Id, IsStart = s.IsStart, IsAccepting = s.IsAccepting })],
            Transitions = [.. pda.Transitions.Select(t => new Transition { FromStateId = t.FromStateId, ToStateId = t.ToStateId, Symbol = t.Symbol, StackPop = t.StackPop, StackPush = t.StackPush })]
        };

        // Run some steps to create stack & history
        var state = pda.StartExecution("aa");
        // push twice
        pda.StepForward(state);
        pda.StepForward(state);

        // state now has position 2, stack with two X above bottom
        var pdaState = state as PDAExecutionState;
        pdaState.ShouldNotBeNull();
        pdaState.Stack.Count.ShouldBeGreaterThan(1);
        pdaState.History.Count.ShouldBeGreaterThan(0);

        // Serialize into model
        execSvc.UpdateModelFromState(model, state);
        model.StackSerialized.ShouldNotBeNullOrEmpty();
        model.StateHistorySerialized.ShouldNotBeNullOrEmpty();

        // Reconstruct state from model
        var reconstructed = execSvc.ReconstructState(model);
        reconstructed.ShouldNotBeOfType<AutomatonExecutionState>();
        var recPda = reconstructed as PDAExecutionState;
        recPda.ShouldNotBeNull();

        // Compare stacks (top-first)
        var origArr = pdaState.Stack.ToArray();
        var recArr = recPda.Stack.ToArray();
        origArr.Length.ShouldBe(recArr.Length);
        for (int i = 0; i < origArr.Length; i++) origArr[i].ShouldBe(recArr[i]);

        // Compare history lengths
        pdaState.History.Count.ShouldBe(recPda.History.Count);
    }

    [Fact]
    public async Task AutomatonFileService_ImportExport_Preserves_PDA_StackInfo()
    {
        var fileSvc = new AutomatonFileService(new NullLogger<AutomatonFileService>());

        var model = new AutomatonViewModel
        {
            Type = AutomatonType.PDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "XY" },
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'b', StackPop = 'X', StackPush = null }
            ]
        };

        var (name, content) = fileSvc.ExportJson(model);
        name.ShouldNotBeNullOrEmpty();
        content.ShouldNotBeNullOrEmpty();

        // create in-memory formfile
        var bytes = Encoding.UTF8.GetBytes(content);
        var formFile = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "upload", "pda.json");

        var (ok, imported, error) = await fileSvc.LoadFromFileAsync(formFile);
        ok.ShouldBeTrue(error);
        imported.ShouldNotBeNull();
        imported!.Type.ShouldBe(AutomatonType.PDA);
        imported.Transitions.ShouldContain(t => t.StackPush == "XY");
        imported.Transitions.ShouldContain(t => t.StackPop.HasValue && t.StackPop.Value == 'X');
    }

    [Fact]
    public void StackPop_Null_And_Epsilon_Are_Equivalent_At_Runtime()
    {
        // Build two PDAs with identical behavior except StackPop null vs '\0'
        var pdaNull = new PDA();
        pdaNull.AddState(new State { Id = 1, IsStart = true, IsAccepting = true });
        pdaNull.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" });
        pdaNull.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'b', StackPop = null, StackPush = null });

        var pdaEps = new PDA();
        pdaEps.AddState(new State { Id = 1, IsStart = true, IsAccepting = true });
        pdaEps.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" });
        pdaEps.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'b', StackPop = '\0', StackPush = null });

        var inputs = new[] { "", "a", "ab", "aab", "b" };
        foreach (var inp in inputs)
        {
            var r1 = pdaNull.Execute(inp);
            var r2 = pdaEps.Execute(inp);
            r1.ShouldBe(r2);
        }
    }

    [Fact]
    public void EpsilonPushLoop_Safety_Limits_Growth()
    {
        // Epsilon loop that always pushes 'A' should not cause infinite loop thanks to safety bounds
        var pda = new PDA();
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        // epsilon that pushes 'A' repeatedly
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = '\0', StackPop = '\0', StackPush = "A" });

        // Execute should terminate and return false
        var ok = pda.Execute("");
        ok.ShouldBeFalse();

        // ExecuteAll should finish and stack size should be bounded by safety limit (1000 iterations in implementation)
        var state = pda.StartExecution("");
        pda.ExecuteAll(state);
        var pdaState = state as PDAExecutionState;
        pdaState.ShouldNotBeNull();
        // Expect stack size not to exceed safety threshold + bottom
        pdaState.Stack.Count.ShouldBeLessThanOrEqualTo(1100);
    }

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
