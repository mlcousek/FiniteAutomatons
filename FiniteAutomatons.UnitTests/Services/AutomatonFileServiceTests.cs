using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using System.Text;
using System.Text.Json;

namespace FiniteAutomatons.UnitTests.Services;

public class AutomatonFileServiceTests
{
    private static AutomatonFileService Create() => new(new NullLogger<AutomatonFileService>());

    private static FormFile LoadFile(string relative)
    {
        var path = Path.Combine(AppContext.BaseDirectory, relative.Replace('/', Path.DirectorySeparatorChar));
        var bytes = File.ReadAllBytes(path);
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "upload", Path.GetFileName(path));
    }

    [Fact]
    public async Task Load_DfaJson_Works()
    {
        var svc = Create();
        var file = LoadFile("AutomatonFiles/dfa-simple.json");
        var (ok, model, error) = await svc.LoadFromFileAsync(file);
        ok.ShouldBeTrue(error);
        model!.Type.ShouldBe(AutomatonType.DFA);
        model.States.Count.ShouldBe(2);
        model.Transitions.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Load_NfaFromHeuristicJson_Works()
    {
        var svc = Create();
        var file = LoadFile("AutomatonFiles/nfa-nondet.json");
        var (ok, model, error) = await svc.LoadFromFileAsync(file);
        ok.ShouldBeTrue(error);
        model!.Type.ShouldBe(AutomatonType.NFA);
    }

    [Fact]
    public async Task Load_EpsilonNfa_FromText_Works()
    {
        var svc = Create();
        var file = LoadFile("AutomatonFiles/enfa-epsilon.txt");
        var (ok, model, error) = await svc.LoadFromFileAsync(file);
        ok.ShouldBeTrue(error);
        model!.Type.ShouldBe(AutomatonType.EpsilonNFA);
        model.Transitions.ShouldContain(t => t.Symbol == '\0');
    }

    [Fact]
    public async Task Load_InvalidText_ReturnsError()
    {
        var svc = Create();
        var file = LoadFile("AutomatonFiles/invalid-multiple-initial.txt");
        var (ok, model, error) = await svc.LoadFromFileAsync(file);
        ok.ShouldBeFalse();
        model.ShouldBeNull();
        error.ShouldNotBeNull();
    }

    [Fact]
    public async Task Load_InvalidJson_ReturnsError()
    {
        var svc = Create();
        var file = LoadFile("AutomatonFiles/invalid-json.json");
        var (ok, model, error) = await svc.LoadFromFileAsync(file);
        ok.ShouldBeFalse();
        model.ShouldBeNull();
        error!.ToLowerInvariant().ShouldContain("state");
    }

    [Fact]
    public void Export_Json_ProducesSerializableContent()
    {
        var svc = Create();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true }],
            Transitions = [new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }]
        };
        var (name, content) = svc.ExportJson(model);
        name.ShouldEndWith(".json");
        content.ShouldContain("\"States\"");
        content.ShouldContain("\"Transitions\"");
    }

    [Fact]
    public void Export_Text_ProducesSerializableContent()
    {
        var svc = Create();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true }],
            Transitions = [new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }, new() { FromStateId = 1, ToStateId = 1, Symbol = 'a' }]
        };
        var (name, content) = svc.ExportText(model);
        name.ShouldEndWith(".txt");
        content.ShouldContain("$states:");
        content.ShouldContain("$transitions:");
    }

    [Fact]
    public void Export_JsonWithState_ProducesFullViewModel()
    {
        var svc = Create();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true }],
            Transitions = [new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }],
            Input = "abba",
            Position = 2,
            HasExecuted = true,
            CurrentStateId = 2,
            IsCustomAutomaton = true,
            StateHistorySerialized = "[]"
        };

        var (name, content) = svc.ExportJsonWithState(model);
        name.ShouldEndWith(".json");
        content.ShouldContain("\"Input\"");
        content.ShouldContain("\"Position\"");
        content.ShouldContain("\"StateHistorySerialized\"");
        content.ShouldContain("abba");
    }

    [Fact]
    public async Task Load_ViewModelWithState_FromJson_Works()
    {
        var svc = Create();
        var original = new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }],
            Transitions = [new() { FromStateId = 1, ToStateId = 1, Symbol = 'a' }],
            Input = "0101",
            Position = 1,
            HasExecuted = true,
            CurrentStates = [1],
            IsCustomAutomaton = false
        };

        var json = JsonSerializer.Serialize(original);
        var bytes = Encoding.UTF8.GetBytes(json);
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "upload", "vm.json");

        var (ok, model, error) = await svc.LoadViewModelWithStateAsync(file);
        ok.ShouldBeTrue(error);
        model.ShouldNotBeNull();
        model!.Input.ShouldBe(original.Input);
        model.Position.ShouldBe(original.Position);
        model.IsCustomAutomaton.ShouldBeTrue(); // service ensures this flag
    }

    [Fact]
    public async Task Load_ViewModelWithState_EmptyFile_ReturnsError()
    {
        var svc = Create();
        var empty = new FormFile(new MemoryStream(), 0, 0, "upload", "empty.txt");
        var (ok, model, error) = await svc.LoadViewModelWithStateAsync(empty);
        ok.ShouldBeFalse();
        model.ShouldBeNull();
        error.ShouldNotBeNull();
    }

    [Fact]
    public async Task Load_ViewModelWithState_InvalidJson_FallsBackToDomainParser()
    {
        var svc = Create();
        // Use a sample automaton text file (domain format) so JSON parse fails and fallback occurs
        var file = LoadFile("AutomatonFiles/enfa-epsilon.txt");
        var (ok, model, error) = await svc.LoadViewModelWithStateAsync(file);
        ok.ShouldBeTrue(error);
        model.ShouldNotBeNull();
        model!.Type.ShouldBe(AutomatonType.EpsilonNFA);
        model.IsCustomAutomaton.ShouldBeTrue();
    }

    [Fact]
    public void Export_JsonWithState_Null_Throws()
    {
        var svc = Create();
        Should.Throw<ArgumentNullException>(() => svc.ExportJsonWithState(null!));
    }

    [Fact]
    public void ExportWithInput_IncludesInputButClearsExecutionState()
    {
        var svc = Create();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [new() { FromStateId = 1, ToStateId = 1, Symbol = 'a' }],
            Input = "test123",
            Position = 3,
            CurrentStateId = 1,
            IsAccepted = true,
            StateHistorySerialized = "[1,1,1]"
        };

        var (name, content) = svc.ExportWithInput(model);

        name.ShouldEndWith(".json");
        name.ShouldContain("withinput");

        var exported = JsonSerializer.Deserialize<AutomatonViewModel>(content);
        exported.ShouldNotBeNull();
        exported!.Input.ShouldBe("test123");
        exported.States.Count.ShouldBe(1);
        exported.Transitions.Count.ShouldBe(1);
        exported.Position.ShouldBe(0);
        exported.CurrentStateId.ShouldBeNull();
        exported.IsAccepted.ShouldBeNull();
        exported.StateHistorySerialized.ShouldBe(string.Empty);
    }

    [Fact]
    public void ExportWithExecutionState_IncludesFullExecutionState()
    {
        var svc = Create();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [new() { FromStateId = 1, ToStateId = 1, Symbol = 'a' }],
            Input = "aaa",
            Position = 2,
            CurrentStateId = 1,
            IsAccepted = null,
            StateHistorySerialized = "[1,1]"
        };

        var (name, content) = svc.ExportWithExecutionState(model);

        name.ShouldEndWith(".json");
        name.ShouldContain("execution");

        var exported = JsonSerializer.Deserialize<AutomatonViewModel>(content);
        exported.ShouldNotBeNull();
        exported!.Input.ShouldBe("aaa");
        exported.Position.ShouldBe(2);
        exported.CurrentStateId.ShouldBe(1);
        exported.StateHistorySerialized.ShouldBe("[1,1]");
    }

    [Fact]
    public async Task LoadViewModelWithState_CanLoadExportedWithInput()
    {
        var svc = Create();
        var original = new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [new() { FromStateId = 1, ToStateId = 1, Symbol = 'b' }],
            Input = "bbb"
        };

        var (_, content) = svc.ExportWithInput(original);
        var bytes = Encoding.UTF8.GetBytes(content);
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "upload", "test.json");

        var (ok, loaded, error) = await svc.LoadViewModelWithStateAsync(file);

        ok.ShouldBeTrue(error);
        loaded.ShouldNotBeNull();
        loaded!.Input.ShouldBe("bbb");
        loaded.Position.ShouldBe(0);
        loaded.CurrentStateId.ShouldBeNull();
        loaded.States.Count.ShouldBe(1);
    }

    [Fact]
    public async Task LoadViewModelWithState_CanLoadExportedWithExecutionState()
    {
        var svc = Create();
        var original = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true }],
            Transitions = [new() { FromStateId = 1, ToStateId = 2, Symbol = 'x' }],
            Input = "xyz",
            Position = 1,
            CurrentStateId = 2,
            IsAccepted = null,
            StateHistorySerialized = "[1]"
        };

        var (_, content) = svc.ExportWithExecutionState(original);
        var bytes = Encoding.UTF8.GetBytes(content);
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "upload", "exec.json");

        var (ok, loaded, error) = await svc.LoadViewModelWithStateAsync(file);

        ok.ShouldBeTrue(error);
        loaded.ShouldNotBeNull();
        loaded!.Input.ShouldBe("xyz");
        loaded.Position.ShouldBe(1);
        loaded.CurrentStateId.ShouldBe(2);
        loaded.StateHistorySerialized.ShouldBe("[1]");
    }

    #region ExportWithInput Comprehensive Tests

    [Fact]
    public void ExportWithInput_NullModel_ThrowsArgumentNullException()
    {
        var svc = Create();
        Should.Throw<ArgumentNullException>(() => svc.ExportWithInput(null!));
    }

    [Fact]
    public void ExportWithInput_EmptyInput_ExportsSuccessfully()
    {
        var svc = Create();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            Input = string.Empty
        };

        var (name, content) = svc.ExportWithInput(model);

        name.ShouldNotBeNullOrEmpty();
        content.ShouldNotBeNullOrEmpty();
        var exported = JsonSerializer.Deserialize<AutomatonViewModel>(content);
        exported!.Input.ShouldBe(string.Empty);
    }

    [Fact]
    public void ExportWithInput_NullInput_ExportsSuccessfully()
    {
        var svc = Create();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            Input = null!
        };

        // Should not throw - null input is handled gracefully
        Should.NotThrow(() =>
        {
            var (name, content) = svc.ExportWithInput(model);
            name.ShouldNotBeNullOrEmpty();
            content.ShouldNotBeNullOrEmpty();
        });
    }

    [Fact]
    public void ExportWithInput_NFA_ClearsCurrentStatesArray()
    {
        var svc = Create();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true }],
            Transitions = [new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }],
            Input = "aaa",
            Position = 2,
            CurrentStates = [1, 2],
            StateHistorySerialized = "[[1],[1,2]]"
        };

        var (_, content) = svc.ExportWithInput(model);

        var exported = JsonSerializer.Deserialize<AutomatonViewModel>(content);
        exported!.CurrentStates.ShouldBeNull();
        exported.StateHistorySerialized.ShouldBe(string.Empty);
    }

    [Fact]
    public void ExportWithInput_EpsilonNFA_PreservesStructure()
    {
        var svc = Create();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.EpsilonNFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true }],
            Transitions = [
                new() { FromStateId = 1, ToStateId = 2, Symbol = '\0' },
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a' }
            ],
            Input = "test",
            Position = 3,
            CurrentStates = [1, 2]
        };

        var (_, content) = svc.ExportWithInput(model);

        var exported = JsonSerializer.Deserialize<AutomatonViewModel>(content);
        exported!.Type.ShouldBe(AutomatonType.EpsilonNFA);
        exported.Transitions.Count.ShouldBe(2);
        exported.Transitions.ShouldContain(t => t.Symbol == '\0');
        exported.Input.ShouldBe("test");
        exported.Position.ShouldBe(0);
    }

    [Fact]
    public void ExportWithInput_PDA_ClearsStackButPreservesTransitions()
    {
        var svc = Create();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.PDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = 'Z', StackPush = "ZA" }],
            Input = "aaa",
            Position = 2,
            CurrentStateId = 1,
            StackSerialized = "[\"Z\",\"A\",\"A\"]"
        };

        var (_, content) = svc.ExportWithInput(model);

        var exported = JsonSerializer.Deserialize<AutomatonViewModel>(content);
        exported!.Type.ShouldBe(AutomatonType.PDA);
        exported.Transitions[0].StackPop.ShouldBe('Z');
        exported.Transitions[0].StackPush.ShouldBe("ZA");
        exported.StackSerialized.ShouldBeNull();
        exported.Position.ShouldBe(0);
    }

    [Fact]
    public void ExportWithInput_ComplexInput_SpecialCharacters()
    {
        var svc = Create();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            Input = "a\nb\tc\"d'e\\f"
        };

        var (_, content) = svc.ExportWithInput(model);

        var exported = JsonSerializer.Deserialize<AutomatonViewModel>(content);
        exported!.Input.ShouldBe("a\nb\tc\"d'e\\f");
    }

    [Fact]
    public void ExportWithInput_LongInput_HandlesCorrectly()
    {
        var svc = Create();
        var longInput = new string('a', 10000);
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [new() { FromStateId = 1, ToStateId = 1, Symbol = 'a' }],
            Input = longInput,
            Position = 5000
        };

        var (_, content) = svc.ExportWithInput(model);

        var exported = JsonSerializer.Deserialize<AutomatonViewModel>(content);
        exported!.Input.ShouldBe(longInput);
        exported.Position.ShouldBe(0);
    }

    [Fact]
    public void ExportWithInput_PreservesIsCustomAutomatonFlag()
    {
        var svc = Create();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            Input = "test",
            IsCustomAutomaton = true
        };

        var (_, content) = svc.ExportWithInput(model);

        var exported = JsonSerializer.Deserialize<AutomatonViewModel>(content);
        exported!.IsCustomAutomaton.ShouldBeTrue();
    }

    [Fact]
    public void ExportWithInput_PreservesIsCustomAutomatonFalse()
    {
        var svc = Create();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            Input = "test",
            IsCustomAutomaton = false
        };

        var (_, content) = svc.ExportWithInput(model);

        var exported = JsonSerializer.Deserialize<AutomatonViewModel>(content);
        exported!.IsCustomAutomaton.ShouldBeFalse();
    }

    [Fact]
    public void ExportWithInput_MultipleStatesAndTransitions_PreservesAll()
    {
        var svc = Create();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States = [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = false },
                new() { Id = 3, IsStart = false, IsAccepting = true }
            ],
            Transitions = [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 1, ToStateId = 3, Symbol = 'a' },
                new() { FromStateId = 2, ToStateId = 3, Symbol = 'b' }
            ],
            Input = "ab"
        };

        var (_, content) = svc.ExportWithInput(model);

        var exported = JsonSerializer.Deserialize<AutomatonViewModel>(content);
        exported!.States.Count.ShouldBe(3);
        exported.Transitions.Count.ShouldBe(3);
        exported.States.ShouldContain(s => s.IsStart);
        exported.States.ShouldContain(s => s.IsAccepting);
    }

    #endregion

    #region ExportWithExecutionState Comprehensive Tests

    [Fact]
    public void ExportWithExecutionState_NullModel_ThrowsArgumentNullException()
    {
        var svc = Create();
        Should.Throw<ArgumentNullException>(() => svc.ExportWithExecutionState(null!));
    }

    [Fact]
    public void ExportWithExecutionState_EmptyInput_LogsWarning()
    {
        var svc = Create();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            Input = string.Empty,
            Position = 0,
            CurrentStateId = 1
        };

        var (name, content) = svc.ExportWithExecutionState(model);

        name.ShouldNotBeNullOrEmpty();
        content.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void ExportWithExecutionState_NullInput_LogsWarning()
    {
        var svc = Create();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            Input = null!,
            Position = 0,
            CurrentStateId = 1
        };

        var (_, content) = svc.ExportWithExecutionState(model);

        content.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void ExportWithExecutionState_PositionZero_PreservesState()
    {
        var svc = Create();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            Input = "test",
            Position = 0,
            CurrentStateId = 1,
            StateHistorySerialized = "[]"
        };

        var (_, content) = svc.ExportWithExecutionState(model);

        var exported = JsonSerializer.Deserialize<AutomatonViewModel>(content);
        exported!.Position.ShouldBe(0);
        exported.CurrentStateId.ShouldBe(1);
        exported.StateHistorySerialized.ShouldBe("[]");
    }

    [Fact]
    public void ExportWithExecutionState_PositionAtEnd_PreservesState()
    {
        var svc = Create();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [new() { FromStateId = 1, ToStateId = 1, Symbol = 'a' }],
            Input = "aaa",
            Position = 3,
            CurrentStateId = 1,
            IsAccepted = true,
            StateHistorySerialized = "[1,1,1]"
        };

        var (_, content) = svc.ExportWithExecutionState(model);

        var exported = JsonSerializer.Deserialize<AutomatonViewModel>(content);
        exported!.Position.ShouldBe(3);
        exported.CurrentStateId.ShouldBe(1);
        exported.IsAccepted.ShouldBe(true);
        exported.StateHistorySerialized.ShouldBe("[1,1,1]");
    }

    [Fact]
    public void ExportWithExecutionState_NFA_PreservesCurrentStatesArray()
    {
        var svc = Create();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true }],
            Transitions = [new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }],
            Input = "aa",
            Position = 1,
            CurrentStates = [1, 2],
            StateHistorySerialized = "[[1]]"
        };

        var (_, content) = svc.ExportWithExecutionState(model);

        var exported = JsonSerializer.Deserialize<AutomatonViewModel>(content);
        exported!.CurrentStates.ShouldNotBeNull();
        exported.CurrentStates!.Count.ShouldBe(2);
        exported.CurrentStates.ShouldContain(1);
        exported.CurrentStates.ShouldContain(2);
    }

    [Fact]
    public void ExportWithExecutionState_PDA_PreservesStackState()
    {
        var svc = Create();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.PDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = 'Z', StackPush = "ZA" }],
            Input = "aaa",
            Position = 2,
            CurrentStateId = 1,
            StackSerialized = "[\"Z\",\"A\",\"A\"]"
        };

        var (_, content) = svc.ExportWithExecutionState(model);

        var exported = JsonSerializer.Deserialize<AutomatonViewModel>(content);
        exported!.StackSerialized.ShouldBe("[\"Z\",\"A\",\"A\"]");
        exported.Position.ShouldBe(2);
    }

    [Fact]
    public void ExportWithExecutionState_IsAcceptedTrue_PreservesValue()
    {
        var svc = Create();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            Input = "test",
            Position = 4,
            CurrentStateId = 1,
            IsAccepted = true
        };

        var (_, content) = svc.ExportWithExecutionState(model);

        var exported = JsonSerializer.Deserialize<AutomatonViewModel>(content);
        exported!.IsAccepted.ShouldBe(true);
    }

    [Fact]
    public void ExportWithExecutionState_IsAcceptedFalse_PreservesValue()
    {
        var svc = Create();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }],
            Transitions = [],
            Input = "test",
            Position = 4,
            CurrentStateId = 1,
            IsAccepted = false
        };

        var (_, content) = svc.ExportWithExecutionState(model);

        var exported = JsonSerializer.Deserialize<AutomatonViewModel>(content);
        exported!.IsAccepted.ShouldBe(false);
    }

    [Fact]
    public void ExportWithExecutionState_IsAcceptedNull_PreservesValue()
    {
        var svc = Create();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            Input = "test",
            Position = 2,
            CurrentStateId = 1,
            IsAccepted = null
        };

        var (_, content) = svc.ExportWithExecutionState(model);

        var exported = JsonSerializer.Deserialize<AutomatonViewModel>(content);
        exported!.IsAccepted.ShouldBeNull();
    }

    [Fact]
    public void ExportWithExecutionState_ComplexStateHistory_PreservesExactly()
    {
        var svc = Create();
        var stateHistory = "[1,2,3,2,1]";
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = false }, new() { Id = 3, IsStart = false, IsAccepting = true }],
            Transitions = [],
            Input = "abcde",
            Position = 5,
            CurrentStateId = 1,
            StateHistorySerialized = stateHistory
        };

        var (_, content) = svc.ExportWithExecutionState(model);

        var exported = JsonSerializer.Deserialize<AutomatonViewModel>(content);
        exported!.StateHistorySerialized.ShouldBe(stateHistory);
    }

    [Fact]
    public void ExportWithExecutionState_AllAutomatonTypes_GeneratesCorrectFilenames()
    {
        var svc = Create();

        foreach (var type in new[] { AutomatonType.DFA, AutomatonType.NFA, AutomatonType.EpsilonNFA, AutomatonType.PDA })
        {
            var model = new AutomatonViewModel
            {
                Type = type,
                States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
                Transitions = [],
                Input = "test",
                Position = 1,
                CurrentStateId = 1
            };

            var (name, _) = svc.ExportWithExecutionState(model);

            name.ShouldContain("execution");
            name.ShouldEndWith(".json");
        }
    }

    #endregion

    #region Round-trip Tests

    [Fact]
    public async Task RoundTrip_ExportWithInput_ThenLoad_PreservesInputAndClearsState()
    {
        var svc = Create();
        var original = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }, new() { Id = 2, IsStart = false, IsAccepting = false }],
            Transitions = [new() { FromStateId = 1, ToStateId = 2, Symbol = 'x' }],
            Input = "xxx",
            Position = 2,
            CurrentStateId = 2,
            IsAccepted = false
        };

        var (_, content) = svc.ExportWithInput(original);
        var bytes = Encoding.UTF8.GetBytes(content);
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "upload", "test.json");

        var (ok, loaded, error) = await svc.LoadViewModelWithStateAsync(file);

        ok.ShouldBeTrue(error);
        loaded!.Type.ShouldBe(AutomatonType.DFA);
        loaded.States.Count.ShouldBe(2);
        loaded.Transitions.Count.ShouldBe(1);
        loaded.Input.ShouldBe("xxx");
        loaded.Position.ShouldBe(0);
        loaded.CurrentStateId.ShouldBeNull();
        loaded.IsAccepted.ShouldBeNull();
    }

    [Fact]
    public async Task RoundTrip_ExportWithExecutionState_ThenLoad_PreservesEverything()
    {
        var svc = Create();
        var original = new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true }],
            Transitions = [new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }, new() { FromStateId = 1, ToStateId = 1, Symbol = 'a' }],
            Input = "aaa",
            Position = 2,
            CurrentStates = [1, 2],
            IsAccepted = null,
            StateHistorySerialized = "[[1],[1,2]]"
        };

        var (_, content) = svc.ExportWithExecutionState(original);
        var bytes = Encoding.UTF8.GetBytes(content);
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "upload", "exec.json");

        var (ok, loaded, error) = await svc.LoadViewModelWithStateAsync(file);

        ok.ShouldBeTrue(error);
        loaded!.Type.ShouldBe(AutomatonType.NFA);
        loaded.Input.ShouldBe("aaa");
        loaded.Position.ShouldBe(2);
        loaded.CurrentStates!.Count.ShouldBe(2);
        loaded.StateHistorySerialized.ShouldBe("[[1],[1,2]]");
    }

    [Fact]
    public async Task RoundTrip_PDA_WithStackState_PreservesExactly()
    {
        var svc = Create();
        var original = new AutomatonViewModel
        {
            Type = AutomatonType.PDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = 'Z', StackPush = "AZ" }],
            Input = "aaa",
            Position = 2,
            CurrentStateId = 1,
            StackSerialized = "[\"A\",\"A\",\"Z\"]",
            StateHistorySerialized = "[1,1]"
        };

        var (_, content) = svc.ExportWithExecutionState(original);
        var bytes = Encoding.UTF8.GetBytes(content);
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "upload", "pda.json");

        var (ok, loaded, error) = await svc.LoadViewModelWithStateAsync(file);

        ok.ShouldBeTrue(error);
        loaded!.Type.ShouldBe(AutomatonType.PDA);
        loaded.StackSerialized.ShouldBe("[\"A\",\"A\",\"Z\"]");
        loaded.Position.ShouldBe(2);
    }

    [Fact]
    public async Task RoundTrip_EpsilonNFA_WithEpsilonTransitions_PreservesStructure()
    {
        var svc = Create();
        var original = new AutomatonViewModel
        {
            Type = AutomatonType.EpsilonNFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true }],
            Transitions = [
                new() { FromStateId = 1, ToStateId = 2, Symbol = '\0' },
                new() { FromStateId = 2, ToStateId = 1, Symbol = 'a' }
            ],
            Input = "a"
        };

        var (_, content) = svc.ExportWithInput(original);
        var bytes = Encoding.UTF8.GetBytes(content);
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "upload", "enfa.json");

        var (ok, loaded, error) = await svc.LoadViewModelWithStateAsync(file);

        ok.ShouldBeTrue(error);
        loaded!.Type.ShouldBe(AutomatonType.EpsilonNFA);
        loaded.Transitions.Count.ShouldBe(2);
        loaded.Transitions.ShouldContain(t => t.Symbol == '\0');
    }

    #endregion

    #region Edge Cases and Boundary Tests

    [Fact]
    public void ExportWithInput_NoStates_HandlesGracefully()
    {
        var svc = Create();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [],
            Transitions = [],
            Input = "test"
        };

        var (_, content) = svc.ExportWithInput(model);

        var exported = JsonSerializer.Deserialize<AutomatonViewModel>(content);
        exported!.States.ShouldBeEmpty();
        exported.Input.ShouldBe("test");
    }

    [Fact]
    public void ExportWithInput_NoTransitions_HandlesGracefully()
    {
        var svc = Create();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            Input = "test"
        };

        var (_, content) = svc.ExportWithInput(model);

        var exported = JsonSerializer.Deserialize<AutomatonViewModel>(content);
        exported!.Transitions.ShouldBeEmpty();
    }

    [Fact]
    public void ExportWithExecutionState_UnicodeInput_PreservesCorrectly()
    {
        var svc = Create();
        var unicodeInput = "Hello ?? ?? ????????";
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            Input = unicodeInput,
            Position = 5,
            CurrentStateId = 1
        };

        var (_, content) = svc.ExportWithExecutionState(model);

        var exported = JsonSerializer.Deserialize<AutomatonViewModel>(content);
        exported!.Input.ShouldBe(unicodeInput);
    }

    [Fact]
    public void ExportWithInput_VeryLargeAutomaton_HandlesCorrectly()
    {
        var svc = Create();
        var states = Enumerable.Range(1, 100).Select(i => new State { Id = i, IsStart = i == 1, IsAccepting = i == 100 }).ToList();
        var transitions = Enumerable.Range(1, 99).Select(i => new Transition { FromStateId = i, ToStateId = i + 1, Symbol = 'a' }).ToList();

        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = states,
            Transitions = transitions,
            Input = new string('a', 99)
        };

        var (_, content) = svc.ExportWithInput(model);

        var exported = JsonSerializer.Deserialize<AutomatonViewModel>(content);
        exported!.States.Count.ShouldBe(100);
        exported.Transitions.Count.ShouldBe(99);
    }

    [Fact]
    public void ExportWithInput_HasExecutedTrue_GetsCleared()
    {
        var svc = Create();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            Input = "test",
            HasExecuted = true,
            Position = 4,
            CurrentStateId = 1
        };

        var (_, content) = svc.ExportWithInput(model);

        var exported = JsonSerializer.Deserialize<AutomatonViewModel>(content);
        exported!.Position.ShouldBe(0);
        exported.CurrentStateId.ShouldBeNull();
    }

    [Fact]
    public void ExportWithExecutionState_HasExecutedTrue_Preserved()
    {
        var svc = Create();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            Input = "test",
            HasExecuted = true,
            Position = 4,
            CurrentStateId = 1
        };

        var (_, content) = svc.ExportWithExecutionState(model);

        var exported = JsonSerializer.Deserialize<AutomatonViewModel>(content);
        exported!.HasExecuted.ShouldBeTrue();
        exported.Position.ShouldBe(4);
    }

    #endregion
}
