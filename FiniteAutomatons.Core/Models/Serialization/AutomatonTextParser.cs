using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using FiniteAutomatons.Core.Utilities;

namespace FiniteAutomatons.Core.Models.Serialization;

public static class AutomatonCustomTextSerializer
{
    private const string CommentPrefix = "#";
    private const string SectionPrefix = "$";

    private const string StatesSection = "$states:";
    private const string InitialSection = "$initial:";
    private const string AcceptingSection = "$accepting:";
    private const string TransitionsSection = "$transitions:";

    private static readonly Regex TransitionPattern = new(
        @"^(?<from>\w+):(?<symbol>.*)>(?<to>\w+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> EpsilonAliases = AutomatonSymbolHelper.EpsilonAliases
        .Concat(["\\0", "\0"]) // ensure legacy sequences remain
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private sealed record ParsedDefinition(
        List<string> States,
        string? Initial,
        List<string> Accepting,
        List<(string from, string symbol, string to, int line)> Transitions,
        List<string> Errors);

    public static Automaton Deserialize(string input)
    {
        if (!TryDeserialize(input, out var automaton, out var errors))
            throw new InvalidOperationException($"Invalid automaton definition: {string.Join("; ", errors)}");
        return automaton!;
    }

    public static bool TryDeserialize(string input, out Automaton? automaton, out List<string> errors)
    {
        automaton = null;
        var parsed = Parse(input);
        errors = parsed.Errors;
        if (errors.Count > 0) return false;

        InferAndBuild(parsed, out automaton);
        return true;
    }

    public static string Serialize(Automaton automaton)
    {
        var sb = new StringBuilder();
        var ordered = automaton.States.OrderBy(s => s.Id).ToList();
        var nameMap = ordered.Select((s, i) => (s, name: $"q{i}")).ToDictionary(x => x.s.Id, x => x.name);

        sb.AppendLine(StatesSection);
        foreach (var n in ordered.Select(s => nameMap[s.Id])) sb.AppendLine(n);

        sb.AppendLine();
        sb.AppendLine(InitialSection);
        var start = automaton.States.FirstOrDefault(s => s.IsStart);
        if (start != null) sb.AppendLine(nameMap[start.Id]);

        sb.AppendLine();
        sb.AppendLine(AcceptingSection);
        foreach (var acc in automaton.States.Where(s => s.IsAccepting)) sb.AppendLine(nameMap[acc.Id]);

        sb.AppendLine();
        sb.AppendLine(TransitionsSection);
        foreach (var t in automaton.Transitions)
        {
            var from = nameMap[t.FromStateId];
            var to = nameMap[t.ToStateId];
            // Use ASCII-safe alias 'eps' for epsilon to avoid glyph loss in non-UTF8 contexts
            string symbol = t.Symbol == '\0' ? "eps" : t.Symbol.ToString();
            sb.AppendLine($"{from}:{symbol}>{to}");
        }
        return sb.ToString();
    }

    // ----------------- Parsing / Validation -----------------

    private static ParsedDefinition Parse(string input)
    {
        var states = new List<string>();
        var accepting = new List<string>();
        var transitions = new List<(string from, string symbol, string to, int line)>();
        string? initial = null;
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(input))
        {
            errors.Add("Input was empty.");
            return new(states, initial, accepting, transitions, errors);
        }

        var lines = input.Replace("\r", string.Empty)
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith(CommentPrefix))
            .ToList();

        string? currentSection = null;
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.StartsWith(SectionPrefix))
            {
                currentSection = line.ToLowerInvariant();
                continue;
            }

            switch (currentSection)
            {
                case StatesSection:
                    if (!states.Contains(line)) states.Add(line);
                    break;
                case InitialSection:
                    if (initial != null && initial != line)
                        errors.Add($"Multiple initial states specified: '{initial}' and '{line}'. Only one allowed.");
                    initial = line;
                    break;
                case AcceptingSection:
                    if (!accepting.Contains(line)) accepting.Add(line);
                    break;
                case TransitionsSection:
                    var match = TransitionPattern.Match(line);
                    if (match.Success)
                        transitions.Add((match.Groups["from"].Value, match.Groups["symbol"].Value, match.Groups["to"].Value, i + 1));
                    else
                        errors.Add($"Invalid transition syntax on line {i + 1}: '{line}'");
                    break;
            }
        }

        if (states.Count == 0) errors.Add("No states defined.");
        if (initial == null) errors.Add("No initial state specified.");
        else if (!states.Contains(initial)) errors.Add($"Initial state '{initial}' not defined in $states section.");

        var stateMap = states.Select((n, i) => (n, id: i + 1)).ToDictionary(x => x.n, x => x.id);

        foreach (var a in accepting.Where(a => !stateMap.ContainsKey(a)))
            errors.Add($"Accepting state '{a}' not defined in $states section.");

        foreach (var (from, symbol, to, line) in transitions)
        {
            if (!stateMap.ContainsKey(from)) errors.Add($"Transition line {line} references unknown 'from' state '{from}'.");
            if (!stateMap.ContainsKey(to)) errors.Add($"Transition line {line} references unknown 'to' state '{to}'.");
            if (string.IsNullOrEmpty(symbol)) errors.Add($"Transition line {line} has empty symbol.");
            else if (symbol.Length > 1 && !IsEpsilon(symbol)) errors.Add($"Transition line {line} symbol '{symbol}' must be a single character or epsilon alias.");
        }

        return new ParsedDefinition(states, initial, accepting, transitions, errors);
    }

    private static void InferAndBuild(ParsedDefinition parsed, out Automaton automaton)
    {
        bool hasEpsilon = parsed.Transitions.Any(t => IsEpsilon(t.symbol));
        bool nondeterministic = parsed.Transitions
            .GroupBy(t => (t.from, symbol: IsEpsilon(t.symbol) ? "ε" : t.symbol))
            .Any(g => g.Count() > 1);

        var stateMap = parsed.States.Select((n, i) => (n, id: i + 1)).ToDictionary(x => x.n, x => x.id);

        if (hasEpsilon)
            automaton = Build<EpsilonNFA>(stateMap, parsed.Initial!, parsed.Accepting, parsed.Transitions);
        else if (nondeterministic)
            automaton = Build<NFA>(stateMap, parsed.Initial!, parsed.Accepting, parsed.Transitions);
        else
            automaton = Build<DFA>(stateMap, parsed.Initial!, parsed.Accepting, parsed.Transitions);
    }

    private static bool IsEpsilon(string symbol) => AutomatonSymbolHelper.IsEpsilon(symbol);

    private static T Build<T>(Dictionary<string, int> stateMap, string initialName, List<string> acceptingNames, List<(string from, string symbol, string to, int line)> raw)
        where T : Automaton, new()
    {
        var automaton = new T();
        foreach (var (name, id) in stateMap)
        {
            automaton.AddState(new State
            {
                Id = id,
                IsStart = name == initialName,
                IsAccepting = acceptingNames.Contains(name)
            });
        }
        foreach (var (from, symbol, to, _) in raw)
        {
            var fromId = stateMap[from];
            var toId = stateMap[to];
            var ch = IsEpsilon(symbol) ? '\0' : symbol[0];
            automaton.AddTransition(fromId, toId, ch);
        }
        automaton.SetStartState(stateMap[initialName]);
        return automaton;
    }
}
