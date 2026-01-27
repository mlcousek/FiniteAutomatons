using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.Serialization;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Core.Utilities;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text;

namespace FiniteAutomatons.Services.Services;

public class AutomatonFileService(ILogger<AutomatonFileService> logger) : IAutomatonFileService
{
    private readonly ILogger<AutomatonFileService> logger = logger;

    public async Task<(bool Ok, AutomatonViewModel? Model, string? Error)> LoadFromFileAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return (false, null, "Empty file.");

        string content;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms);
            content = Encoding.UTF8.GetString(ms.ToArray());
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        Automaton? automaton = null;
        var textErrors = new List<string>();
        string? jsonError = null;
        bool ok = false;
        try
        {
            if (ext == ".json")
            {
                ok = AutomatonJsonSerializer.TryDeserialize(content, out automaton, out jsonError);
            }
            else
            {
                ok = AutomatonCustomTextSerializer.TryDeserialize(content, out automaton, out textErrors);
                if (!ok && ext == ".txt")
                {
                    ok = AutomatonJsonSerializer.TryDeserialize(content, out automaton, out jsonError);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse automaton file {Name}", file.FileName);
            return (false, null, "Failed to parse automaton file.");
        }

        if (!ok || automaton == null)
        {
            var err = jsonError ?? string.Join("; ", textErrors);
            return (false, null, string.IsNullOrWhiteSpace(err) ? "Invalid automaton file." : err);
        }

        var vm = new AutomatonViewModel
        {
            Type = automaton switch
            {
                EpsilonNFA => AutomatonType.EpsilonNFA,
                NFA => AutomatonType.NFA,
                DFA => AutomatonType.DFA,
                PDA => AutomatonType.PDA,
                _ => AutomatonType.DFA
            },
            States = [.. automaton.States.Select(s => new State { Id = s.Id, IsStart = s.IsStart, IsAccepting = s.IsAccepting })],
            Transitions = [.. automaton.Transitions.Select(t => new Transition { FromStateId = t.FromStateId, ToStateId = t.ToStateId, Symbol = t.Symbol, StackPop = t.StackPop, StackPush = t.StackPush })],
            IsCustomAutomaton = true
        };

        vm.NormalizeEpsilonTransitions();
        logger.LogInformation("Loaded automaton from file {Name}: Type={Type} States={States} Transitions={Trans}", file.FileName, vm.Type, vm.States.Count, vm.Transitions.Count);
        return (true, vm, null);
    }

    public async Task<(bool Ok, AutomatonViewModel? Model, string? Error)> LoadViewModelWithStateAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return (false, null, "Empty file.");

        string content;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms);
            content = Encoding.UTF8.GetString(ms.ToArray());
        }

        try
        {
            var vm = System.Text.Json.JsonSerializer.Deserialize<AutomatonViewModel>(content);
            if (vm != null)
            {
                vm.IsCustomAutomaton = true;
                vm.NormalizeEpsilonTransitions();
                return (true, vm, null);
            }
        }
        catch (System.Text.Json.JsonException ex)
        {
            logger.LogDebug(ex, "Uploaded file is not a full viewmodel JSON, falling back to domain parser.");
        }

        // Fallback to domain parsing
        return await LoadFromFileAsync(file);
    }

    public (string FileName, string Content) ExportJson(AutomatonViewModel model)
    {
        var automaton = BuildAutomaton(model);
        var json = AutomatonJsonSerializer.Serialize(automaton);
        var name = $"automaton-{DateTime.UtcNow:yyyyMMddHHmmss}.json";
        return (name, json);
    }

    public (string FileName, string Content) ExportJsonWithState(AutomatonViewModel model)
    {
        model = model ?? throw new ArgumentNullException(nameof(model));
        var content = System.Text.Json.JsonSerializer.Serialize(model, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        var name = $"automaton-withstate-{DateTime.UtcNow:yyyyMMddHHmmss}.json";
        return (name, content);
    }

    public (string FileName, string Content) ExportText(AutomatonViewModel model)
    {
        var automaton = BuildAutomaton(model);
        var text = AutomatonCustomTextSerializer.Serialize(automaton);
        var name = $"automaton-{DateTime.UtcNow:yyyyMMddHHmmss}.txt";
        return (name, text);
    }

    private static Automaton BuildAutomaton(AutomatonViewModel model)
    {
        Automaton automaton = model.Type switch
        {
            AutomatonType.DFA => new DFA(),
            AutomatonType.NFA => new NFA(),
            AutomatonType.EpsilonNFA => new EpsilonNFA(),
            AutomatonType.PDA => new PDA(),
            _ => new DFA()
        };
        foreach (var s in model.States)
            automaton.AddState(new State { Id = s.Id, IsStart = s.IsStart, IsAccepting = s.IsAccepting });
        foreach (var t in model.Transitions)
            automaton.AddTransition(new Transition { FromStateId = t.FromStateId, ToStateId = t.ToStateId, Symbol = t.Symbol, StackPop = t.StackPop, StackPush = t.StackPush });
        var start = model.States.FirstOrDefault(s => s.IsStart);
        if (start != null)
            automaton.SetStartState(start.Id);
        return automaton;
    }
}
