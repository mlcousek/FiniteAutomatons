using System.Text; 
using Microsoft.AspNetCore.Http; 
using Microsoft.Extensions.Logging; 
using FiniteAutomatons.Services.Interfaces; 
using FiniteAutomatons.Core.Models.Serialization; 
using FiniteAutomatons.Core.Models.ViewModel; 
using FiniteAutomatons.Core.Models.DoMain; 
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons; 
using FiniteAutomatons.Core.Utilities; 

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
        List<string> textErrors = [];
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
            Type = automaton is EpsilonNFA ? AutomatonType.EpsilonNFA : automaton is NFA ? AutomatonType.NFA : AutomatonType.DFA,
            States = automaton.States.Select(s => new State { Id = s.Id, IsStart = s.IsStart, IsAccepting = s.IsAccepting }).ToList(),
            Transitions = automaton.Transitions.Select(t => new Transition { FromStateId = t.FromStateId, ToStateId = t.ToStateId, Symbol = t.Symbol }).ToList(),
            IsCustomAutomaton = true
        };

        vm.NormalizeEpsilonTransitions();
        logger.LogInformation("Loaded automaton from file {Name}: Type={Type} States={States} Transitions={Trans}", file.FileName, vm.Type, vm.States.Count, vm.Transitions.Count);
        return (true, vm, null);
    }

    public (string FileName, string Content) ExportJson(AutomatonViewModel model)
    {
        var automaton = BuildAutomaton(model);
        var json = AutomatonJsonSerializer.Serialize(automaton);
        var name = $"automaton-{DateTime.UtcNow:yyyyMMddHHmmss}.json";
        return (name, json);
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
            _ => new DFA()
        };
        foreach (var s in model.States)
            automaton.AddState(new State { Id = s.Id, IsStart = s.IsStart, IsAccepting = s.IsAccepting });
        foreach (var t in model.Transitions)
            automaton.AddTransition(t.FromStateId, t.ToStateId, t.Symbol);
        var start = model.States.FirstOrDefault(s => s.IsStart);
        if (start != null)
            automaton.SetStartState(start.Id);
        return automaton;
    }
}
