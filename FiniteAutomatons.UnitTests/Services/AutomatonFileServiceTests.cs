using FiniteAutomatons.Services.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using FiniteAutomatons.Core.Models.ViewModel;

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
            States = [ new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true } ],
            Transitions = [ new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' } ]
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
            States = [ new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true } ],
            Transitions = [ new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }, new() { FromStateId = 1, ToStateId = 1, Symbol = 'a' } ]
        };
        var (name, content) = svc.ExportText(model);
        name.ShouldEndWith(".txt");
        content.ShouldContain("$states:");
        content.ShouldContain("$transitions:");
    }
}
