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
}
