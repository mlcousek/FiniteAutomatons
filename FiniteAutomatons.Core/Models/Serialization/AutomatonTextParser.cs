using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Core.Utilities;
using System.Text;
using System.Text.RegularExpressions;

namespace FiniteAutomatons.Core.Models.Serialization;

public static class AutomatonCustomTextSerializer
{
    private const string CommentPrefix = "#";
    private const string SectionPrefix = "$";

    private const string TypeSection = "$type:";
    private const string StatesSection = "$states:";
    private const string InitialSection = "$initial:";
    private const string AcceptingSection = "$accepting:";
    private const string TransitionsSection = "$transitions:";

    private static readonly Regex TransitionPattern = new(
        @"^(?<from>\w+):(?<symbol>[^,>]+?)(?:,(?<stackPop>[^/>]*?)(?:/(?<stackPush>.*))?)?>(?<to>\w+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private sealed record ParsedDefinition(
        string? ExplicitType,
        List<string> States,
        string? Initial,
        List<string> Accepting,
        List<(string from, string symbol, string to, string? stackPop, string? stackPush, int line)> Transitions,
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

        sb.AppendLine(TypeSection);
        sb.AppendLine(ResolveTypeName(automaton));
        sb.AppendLine();

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
            char symbol = t.Symbol == '\0' ? 'ε' : t.Symbol;
            if (automaton is DPDA or NPDA)
            {
                var pop = t.StackPop.HasValue ? (t.StackPop.Value == '\0' ? "ε" : t.StackPop.Value.ToString()) : "ε";
                var push = string.IsNullOrEmpty(t.StackPush)
                    ? "ε"
                    : t.StackPush!.Replace("\0", "ε", StringComparison.Ordinal);
                sb.AppendLine($"{from}:{symbol},{pop}/{push}>{to}");
            }
            else
            {
                sb.AppendLine($"{from}:{symbol}>{to}");
            }
        }
        return sb.ToString();
    }

    private static ParsedDefinition Parse(string input)
    {
        string? explicitType = null;
        var states = new List<string>();
        var accepting = new List<string>();
        var transitions = new List<(string from, string symbol, string to, string? stackPop, string? stackPush, int line)>();
        string? initial = null;
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(input))
        {
            errors.Add("Input was empty.");
            return new(explicitType, states, initial, accepting, transitions, errors);
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
                case TypeSection:
                    if (explicitType != null && !string.Equals(explicitType, line, StringComparison.OrdinalIgnoreCase))
                        errors.Add($"Multiple types specified: '{explicitType}' and '{line}'. Only one allowed.");
                    explicitType = line;
                    break;
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
                    {
                        transitions.Add((
                            match.Groups["from"].Value,
                            match.Groups["symbol"].Value,
                            match.Groups["to"].Value,
                            match.Groups["stackPop"].Success ? match.Groups["stackPop"].Value : null,
                            match.Groups["stackPush"].Success ? match.Groups["stackPush"].Value : null,
                            i + 1));
                    }
                    else
                        errors.Add($"Invalid transition syntax on line {i + 1}: '{line}'");
                    break;
            }
        }

        ValidateExplicitType(explicitType, errors);

        if (states.Count == 0) errors.Add("No states defined.");
        if (initial == null) errors.Add("No initial state specified.");
        else if (!states.Contains(initial)) errors.Add($"Initial state '{initial}' not defined in $states section.");

        var stateMap = states.Select((n, i) => (n, id: i + 1)).ToDictionary(x => x.n, x => x.id);

        foreach (var a in accepting.Where(a => !stateMap.ContainsKey(a)))
            errors.Add($"Accepting state '{a}' not defined in $states section.");

        foreach (var (from, symbol, to, stackPop, _, line) in transitions)
        {
            if (!stateMap.ContainsKey(from)) errors.Add($"Transition line {line} references unknown 'from' state '{from}'.");
            if (!stateMap.ContainsKey(to)) errors.Add($"Transition line {line} references unknown 'to' state '{to}'.");
            if (string.IsNullOrEmpty(symbol)) errors.Add($"Transition line {line} has empty symbol.");
            else if (symbol.Length > 1 && !IsEpsilon(symbol)) errors.Add($"Transition line {line} symbol '{symbol}' must be a single character or epsilon (ε).");

            if (stackPop != null && stackPop.Length > 1 && !IsEpsilon(stackPop))
                errors.Add($"Transition line {line} stack pop '{stackPop}' must be a single character or epsilon (ε).");
        }

        return new ParsedDefinition(explicitType, states, initial, accepting, transitions, errors);
    }

    private static void InferAndBuild(ParsedDefinition parsed, out Automaton automaton)
    {
        if (TryResolveExplicitType(parsed.ExplicitType, out var explicitType))
        {
            automaton = explicitType switch
            {
                AutomatonType.EpsilonNFA => Build<EpsilonNFA>(stateMap: parsed.States.Select((n, i) => (n, id: i + 1)).ToDictionary(x => x.n, x => x.id), parsed.Initial!, parsed.Accepting, parsed.Transitions),
                AutomatonType.NFA => Build<NFA>(stateMap: parsed.States.Select((n, i) => (n, id: i + 1)).ToDictionary(x => x.n, x => x.id), parsed.Initial!, parsed.Accepting, parsed.Transitions),
                AutomatonType.DFA => Build<DFA>(stateMap: parsed.States.Select((n, i) => (n, id: i + 1)).ToDictionary(x => x.n, x => x.id), parsed.Initial!, parsed.Accepting, parsed.Transitions),
                AutomatonType.DPDA => Build<DPDA>(stateMap: parsed.States.Select((n, i) => (n, id: i + 1)).ToDictionary(x => x.n, x => x.id), parsed.Initial!, parsed.Accepting, parsed.Transitions),
                AutomatonType.NPDA => Build<NPDA>(stateMap: parsed.States.Select((n, i) => (n, id: i + 1)).ToDictionary(x => x.n, x => x.id), parsed.Initial!, parsed.Accepting, parsed.Transitions),
                _ => Build<DFA>(stateMap: parsed.States.Select((n, i) => (n, id: i + 1)).ToDictionary(x => x.n, x => x.id), parsed.Initial!, parsed.Accepting, parsed.Transitions)
            };
            return;
        }

        bool hasEpsilon = parsed.Transitions.Any(t => IsEpsilon(t.symbol));
        bool hasStackInfo = parsed.Transitions.Any(t => t.stackPop != null || t.stackPush != null);
        bool nondeterministic = parsed.Transitions
            .GroupBy(t => (t.from, symbol: IsEpsilon(t.symbol) ? "ε" : t.symbol, stackPop: NormalizeStackToken(t.stackPop) ?? "*"))
            .Any(g => g.Count() > 1);

        var stateMap = parsed.States.Select((n, i) => (n, id: i + 1)).ToDictionary(x => x.n, x => x.id);

        if (hasStackInfo)
            automaton = nondeterministic
                ? Build<NPDA>(stateMap, parsed.Initial!, parsed.Accepting, parsed.Transitions)
                : Build<DPDA>(stateMap, parsed.Initial!, parsed.Accepting, parsed.Transitions);
        else if (hasEpsilon)
            automaton = Build<EpsilonNFA>(stateMap, parsed.Initial!, parsed.Accepting, parsed.Transitions);
        else if (nondeterministic)
            automaton = Build<NFA>(stateMap, parsed.Initial!, parsed.Accepting, parsed.Transitions);
        else
            automaton = Build<DFA>(stateMap, parsed.Initial!, parsed.Accepting, parsed.Transitions);
    }

    private static bool IsEpsilon(string symbol) => AutomatonSymbolHelper.IsEpsilon(symbol);

    private static T Build<T>(Dictionary<string, int> stateMap, string initialName, List<string> acceptingNames, List<(string from, string symbol, string to, string? stackPop, string? stackPush, int line)> raw)
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
        foreach (var (from, symbol, to, stackPopRaw, stackPushRaw, _) in raw)
        {
            var fromId = stateMap[from];
            var toId = stateMap[to];
            var transition = new Transition
            {
                FromStateId = fromId,
                ToStateId = toId,
                Symbol = IsEpsilon(symbol) ? '\0' : symbol[0]
            };

            var normalizedPop = NormalizeStackToken(stackPopRaw);
            if (normalizedPop != null)
                transition.StackPop = IsEpsilon(normalizedPop) ? '\0' : normalizedPop[0];

            var normalizedPush = NormalizeStackToken(stackPushRaw);
            if (normalizedPush != null)
                transition.StackPush = IsEpsilon(normalizedPush) ? null : normalizedPush;

            automaton.AddTransition(transition);
        }
        automaton.SetStartState(stateMap[initialName]);
        return automaton;
    }

    private static void ValidateExplicitType(string? explicitType, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(explicitType))
            return;

        if (!TryResolveExplicitType(explicitType, out _))
            errors.Add($"Unsupported automaton type '{explicitType}'. Supported: DFA, NFA, EpsilonNFA, DPDA, NPDA.");
    }

    private static bool TryResolveExplicitType(string? typeText, out AutomatonType automatonType)
    {
        automatonType = AutomatonType.DFA;
        if (string.IsNullOrWhiteSpace(typeText))
            return false;

        if (Enum.TryParse<AutomatonType>(typeText, true, out automatonType))
            return true;

        if (string.Equals(typeText, "ε-NFA", StringComparison.OrdinalIgnoreCase)
            || string.Equals(typeText, "Epsilon-NFA", StringComparison.OrdinalIgnoreCase))
        {
            automatonType = AutomatonType.EpsilonNFA;
            return true;
        }

        return false;
    }

    private static string ResolveTypeName(Automaton automaton)
    {
        return automaton switch
        {
            EpsilonNFA => nameof(AutomatonType.EpsilonNFA),
            NFA => nameof(AutomatonType.NFA),
            DFA => nameof(AutomatonType.DFA),
            DPDA => nameof(AutomatonType.DPDA),
            NPDA => nameof(AutomatonType.NPDA),
            _ => nameof(AutomatonType.DFA)
        };
    }

    private static string? NormalizeStackToken(string? token)
    {
        if (token is null)
            return null;

        var trimmed = token.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
