using FiniteAutomatons.Core.Models.Api;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Text;

namespace FiniteAutomatons.UnitTests.TestHelpers;

public sealed class MockSession : ISession
{
    private readonly Dictionary<string, byte[]> store = [];

    public bool IsAvailable => true;
    public string Id { get; } = Guid.NewGuid().ToString();
    public IEnumerable<string> Keys => store.Keys;

    public void Clear() => store.Clear();
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Remove(string key) => store.Remove(key);

    public void Set(string key, byte[] value) => store[key] = value;

    public bool TryGetValue(string key, out byte[] value)
    {
        if (store.TryGetValue(key, out var v)) { value = v; return true; }
        value = [];
        return false;
    }

    public string? GetString(string key)
    {
        if (!TryGetValue(key, out var bytes) || bytes.Length == 0) return null;
        return Encoding.UTF8.GetString(bytes);
    }

    public void SetString(string key, string value)
        => Set(key, Encoding.UTF8.GetBytes(value));
}

public sealed class NoOpLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
        TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    { }
}

public sealed class MockCanvasMappingService : ICanvasMappingService
{
    public CanvasSyncResponse BuildSyncResponse(CanvasSyncRequest request)
    {
        var vm = BuildAutomatonViewModel(request);
        return new CanvasSyncResponse
        {
            Alphabet = vm.Alphabet.Select(c => c == '\0' ? "ε" : c.ToString()).OrderBy(s => s).ToList(),
            HasEpsilonTransitions = vm.Transitions.Any(t => t.Symbol == '\0'),
            IsPDA = vm.Type == AutomatonType.PDA,
            StateCount = vm.States.Count,
            TransitionCount = vm.Transitions.Count,
            States = vm.States.OrderBy(s => s.Id).Select(s => new CanvasSyncStateDto
            {
                Id = s.Id,
                IsStart = s.IsStart,
                IsAccepting = s.IsAccepting
            }).ToList(),
            Transitions = vm.Transitions.OrderBy(t => t.FromStateId).Select(t => new CanvasSyncTransitionDto
            {
                FromStateId = t.FromStateId,
                ToStateId = t.ToStateId,
                SymbolDisplay = t.Symbol == '\0' ? "ε" : t.Symbol.ToString(),
                StackPopDisplay = t.StackPop == '\0' ? "ε" : t.StackPop?.ToString(),
                StackPush = t.StackPush,
                IsPDA = vm.Type == AutomatonType.PDA
            }).ToList()
        };
    }

    public AutomatonViewModel BuildAutomatonViewModel(CanvasSyncRequest request)
    {
        var type = ParseType(request.Type);
        var isPDA = type == AutomatonType.PDA;

        return new AutomatonViewModel
        {
            Type = type,
            States = request.States.Select(s => new Core.Models.DoMain.State
            {
                Id = s.Id,
                IsStart = s.IsStart,
                IsAccepting = s.IsAccepting
            }).ToList(),
            Transitions = request.Transitions.Select(t => new Core.Models.DoMain.Transition
            {
                FromStateId = t.FromStateId,
                ToStateId = t.ToStateId,
                Symbol = ParseSymbol(t.Symbol),
                StackPop = isPDA ? ParseStackPop(t.StackPop) : null,
                StackPush = isPDA ? (string.IsNullOrEmpty(t.StackPush) ? "" : t.StackPush) : null
            }).ToList(),
            IsCustomAutomaton = true
        };
    }

    private static AutomatonType ParseType(string? type)
    {
        return type?.ToUpperInvariant() switch
        {
            "DFA" => AutomatonType.DFA,
            "NFA" => AutomatonType.NFA,
            "EPSILONNFA" => AutomatonType.EpsilonNFA,
            "PDA" => AutomatonType.PDA,
            _ => AutomatonType.DFA
        };
    }

    private static char ParseSymbol(string? symbol)
    {
        if (string.IsNullOrEmpty(symbol) || symbol == "\\0" || symbol == "ε" || symbol == "epsilon")
            return '\0';
        return symbol[0];
    }

    private static char? ParseStackPop(string? stackPop)
    {
        if (string.IsNullOrEmpty(stackPop))
            return null;
        if (stackPop == "\\0" || stackPop == "ε" || stackPop == "epsilon")
            return '\0';
        return stackPop[0];
    }
}
