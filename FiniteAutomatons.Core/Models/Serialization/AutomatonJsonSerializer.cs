using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.ViewModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FiniteAutomatons.Core.Models.Serialization;

public static class AutomatonJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // keep ε unescaped
    };

    private static readonly HashSet<string> EpsilonTokens = new(StringComparer.OrdinalIgnoreCase)
    { "ε", "\0" };

    private class StateDto
    {
        public int Id { get; set; }
        public bool IsStart { get; set; }
        public bool IsAccepting { get; set; }
    }
    private class TransitionDto
    {
        public int FromStateId { get; set; }
        public int ToStateId { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string? StackPop { get; set; } // PDA only
        public string? StackPush { get; set; } // PDA only
    }
    private class AutomatonDto
    {
        public int Version { get; set; } = 1;
        public string? Type { get; set; }
        public List<StateDto> States { get; set; } = [];
        public List<TransitionDto> Transitions { get; set; } = [];
    }

    public static string Serialize(Automaton automaton)
    {
        string type = automaton switch
        {
            EpsilonNFA => nameof(AutomatonType.EpsilonNFA),
            NFA => nameof(AutomatonType.NFA),
            DFA => nameof(AutomatonType.DFA),
            PDA => nameof(AutomatonType.PDA),
            _ => automaton.GetType().Name
        };

        var dto = new AutomatonDto
        {
            Type = type,
            States = [.. automaton.States.Select(s => new StateDto
            {
                Id = s.Id,
                IsStart = s.IsStart,
                IsAccepting = s.IsAccepting
            })],
            Transitions = [.. automaton.Transitions.Select(t => new TransitionDto
            {
                FromStateId = t.FromStateId,
                ToStateId = t.ToStateId,
                Symbol = t.Symbol == '\0' ? "ε" : t.Symbol.ToString(),
                StackPop = t.StackPop.HasValue ? (t.StackPop.Value == '\0' ? "ε" : t.StackPop.Value.ToString()) : null,
                StackPush = t.StackPush
            })]
        };
        return JsonSerializer.Serialize(dto, Options);
    }

    public static Automaton Deserialize(string json)
        => TryDeserialize(json, out var automaton, out var error)
            ? automaton!
            : throw new InvalidOperationException(error ?? "Invalid JSON automaton definition.");

    public static bool TryDeserialize(string json, out Automaton? automaton, out string? error)
    {
        automaton = null;
        error = null;
        AutomatonDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<AutomatonDto>(json, Options);
        }
        catch (JsonException ex)
        {
            error = $"JSON parse error: {ex.Message}";
            return false;
        }

        if (dto == null)
        {
            error = "Empty automaton payload.";
            return false;
        }

        if (dto.States.Count == 0)
        {
            error = "Automaton must contain at least one state.";
            return false;
        }

        string resolvedType = dto.Type ?? ResolveTypeHeuristically(dto);

        automaton = resolvedType switch
        {
            nameof(AutomatonType.EpsilonNFA) => BuildAutomaton<EpsilonNFA>(dto),
            nameof(AutomatonType.NFA) => BuildAutomaton<NFA>(dto),
            nameof(AutomatonType.DFA) => BuildAutomaton<DFA>(dto),
            nameof(AutomatonType.PDA) => BuildAutomaton<PDA>(dto),
            _ => BuildAutomaton<DFA>(dto)
        };
        return true;
    }

    private static string ResolveTypeHeuristically(AutomatonDto dto)
    {
        bool hasStackInfo = dto.Transitions.Any(t => t.StackPop != null || t.StackPush != null);
        if (hasStackInfo) return nameof(AutomatonType.PDA);

        bool hasEpsilon = dto.Transitions.Any(t => EpsilonTokens.Contains(t.Symbol));
        if (hasEpsilon) return nameof(AutomatonType.EpsilonNFA);

        bool nondeterministic = dto.Transitions
            .GroupBy(t => (t.FromStateId, t.Symbol))
            .Any(g => g.Count() > 1);
        return nondeterministic ? nameof(AutomatonType.NFA) : nameof(AutomatonType.DFA);
    }

    private static T BuildAutomaton<T>(AutomatonDto dto) where T : Automaton, new()
    {
        var automaton = new T();
        foreach (var s in dto.States)
        {
            automaton.AddState(new State
            {
                Id = s.Id,
                IsStart = s.IsStart,
                IsAccepting = s.IsAccepting
            });
        }
        foreach (var t in dto.Transitions)
        {
            char sym = EpsilonTokens.Contains(t.Symbol) ? '\0' : t.Symbol[0];
            var transition = new Transition
            {
                FromStateId = t.FromStateId,
                ToStateId = t.ToStateId,
                Symbol = sym,
                StackPop = string.IsNullOrEmpty(t.StackPop) ? null : (EpsilonTokens.Contains(t.StackPop) ? '\0' : t.StackPop[0]),
                StackPush = t.StackPush
            };
            automaton.AddTransition(transition);
        }
        var start = dto.States.FirstOrDefault(s => s.IsStart);
        if (start != null)
            automaton.SetStartState(start.Id);
        return automaton;
    }
}
