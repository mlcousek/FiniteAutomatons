using FiniteAutomatons.Core.Models.Database;
using FiniteAutomatons.Core.Models.DTOs;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Data;
using FiniteAutomatons.Data.Seeding;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Text.Json;

namespace FiniteAutomatons.IntegrationTests.Seeding;

[Collection("Integration Tests")]
public class DemoDataSeederIntegrationTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    private static readonly JsonSerializerOptions DefaultJsonOptions = new();

    // ── Helpers ────────────────────────────────────────────────────────────────

    private ApplicationDbContext GetDb()
    {
        var scope = GetServiceScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    private T GetService<T>() where T : notnull
    {
        var scope = GetServiceScope();
        return scope.ServiceProvider.GetRequiredService<T>();
    }

    private static AutomatonViewModel BuildViewModel(AutomatonPayloadDto payload, string input = "") =>
        new()
        {
            Type = payload.Type,
            States = payload.States ?? [],
            Transitions = payload.Transitions ?? [],
            Input = input,
            IsCustomAutomaton = true
        };

    private static AutomatonPayloadDto? TryLoadContentJson(string contentJson)
    {
        try { return JsonSerializer.Deserialize<AutomatonPayloadDto>(contentJson, DefaultJsonOptions); }
        catch { return null; }
    }

    // ── Seeding verification ────────────────────────────────────────────────────

    [Fact]
    public async Task DemoDataIsSeeded_FiveUsersExist()
    {
        using var db = GetDb();
        var users = await db.Users.ToListAsync();
        users.Count.ShouldBeGreaterThanOrEqualTo(5);

        var emails = users.Select(u => u.Email).ToHashSet(StringComparer.OrdinalIgnoreCase);
        emails.ShouldContain("supervisor@test.test");
        emails.ShouldContain("alice@test.test");
        emails.ShouldContain("bob@test.test");
        emails.ShouldContain("charlie@test.test");
        emails.ShouldContain("diana@test.test");
    }

    [Fact]
    public async Task DemoDataIsSeeded_TwentyTwoSavedAutomatonsExist()
    {
        using var db = GetDb();
        var count = await db.SavedAutomatons.CountAsync();
        count.ShouldBeGreaterThanOrEqualTo(22);
    }

    [Fact]
    public async Task DemoDataIsSeeded_SharedGroupsExist()
    {
        using var db = GetDb();
        var groups = await db.SharedAutomatonGroups.ToListAsync();
        groups.Count.ShouldBeGreaterThanOrEqualTo(3);

        var names = groups.Select(g => g.Name).ToHashSet();
        names.ShouldContain("Formal Language Theory");
        names.ShouldContain("PDA Studies");
        names.ShouldContain("Team Playground");
    }

    [Fact]
    public async Task DemoDataIsSeeded_PendingInvitationExists()
    {
        using var db = GetDb();
        var pending = await db.SharedAutomatonGroupInvitations
            .Where(i => i.Status == InvitationStatus.Pending)
            .CountAsync();
        pending.ShouldBeGreaterThanOrEqualTo(1);
    }

    // ── ContentJson loading ────────────────────────────────────────────────────

    [Fact]
    public async Task AllSavedAutomatons_ContentJsonDeserializesToPayloadDto()
    {
        using var db = GetDb();
        var automatons = await db.SavedAutomatons.ToListAsync();

        foreach (var automaton in automatons)
        {
            var payload = TryLoadContentJson(automaton.ContentJson);
            payload.ShouldNotBeNull($"ContentJson for '{automaton.Name}' failed to deserialize");
            payload!.States.ShouldNotBeNull($"States null for '{automaton.Name}'");
            payload.States!.Count.ShouldBeGreaterThan(0, $"No states in '{automaton.Name}'");
            payload.Transitions.ShouldNotBeNull($"Transitions null for '{automaton.Name}'");
        }
    }

    [Fact]
    public async Task AllSharedAutomatons_ContentJsonDeserializesToPayloadDto()
    {
        using var db = GetDb();
        var automatons = await db.SharedAutomatons.ToListAsync();

        foreach (var automaton in automatons)
        {
            var payload = TryLoadContentJson(automaton.ContentJson);
            payload.ShouldNotBeNull($"ContentJson for '{automaton.Name}' failed to deserialize");
            payload.States!.Count.ShouldBeGreaterThan(0, $"No states in '{automaton.Name}'");
        }
    }

    [Theory]
    [InlineData("DFA – Even number of a's", AutomatonType.DFA, 2)]
    [InlineData("DFA – Binary numbers divisible by 3", AutomatonType.DFA, 3)]
    [InlineData("NFA – Contains substring 'ab'", AutomatonType.NFA, 3)]
    [InlineData("ε-NFA – a(b|c)", AutomatonType.EpsilonNFA, 5)]
    [InlineData("DPDA – aⁿbⁿ (n ≥ 0)", AutomatonType.DPDA, 2)]
    [InlineData("NPDA – Even-length palindromes", AutomatonType.NPDA, 2)]
    public async Task SavedAutomaton_LoadedType_MatchesExpected(string name, AutomatonType expectedType, int expectedStateCount)
    {
        using var db = GetDb();
        var automaton = await db.SavedAutomatons.FirstOrDefaultAsync(a => a.Name == name);
        automaton.ShouldNotBeNull($"'{name}' not found in DB");

        var payload = TryLoadContentJson(automaton!.ContentJson);
        payload.ShouldNotBeNull();
        payload!.Type.ShouldBe(expectedType, $"Wrong type for '{name}'");
        payload.States!.Count.ShouldBe(expectedStateCount, $"Wrong state count for '{name}'");
    }

    // ── WithInput mode verification ────────────────────────────────────────────

    [Fact]
    public async Task SavedAutomaton_WithInput_HasExecutionStateJson()
    {
        using var db = GetDb();
        var withInput = await db.SavedAutomatons
            .Where(a => a.SaveMode == AutomatonSaveMode.WithInput)
            .ToListAsync();

        withInput.Count.ShouldBeGreaterThanOrEqualTo(4, "Expected at least 4 WithInput automatons");

        foreach (var a in withInput)
        {
            a.ExecutionStateJson.ShouldNotBeNullOrWhiteSpace($"'{a.Name}' has WithInput mode but no ExecutionStateJson");

            using var doc = JsonDocument.Parse(a.ExecutionStateJson!);
            doc.RootElement.TryGetProperty("Input", out var inputProp).ShouldBeTrue($"'{a.Name}' ExecutionStateJson has no Input property");
            inputProp.GetString().ShouldNotBeNullOrEmpty($"'{a.Name}' has empty Input in ExecutionStateJson");
        }
    }

    [Fact]
    public async Task DfaBinaryDiv3_WithInput_InputIs110()
    {
        using var db = GetDb();
        var automaton = await db.SavedAutomatons
            .FirstOrDefaultAsync(a => a.Name == "DFA – Binary numbers divisible by 3");

        automaton.ShouldNotBeNull();
        automaton!.SaveMode.ShouldBe(AutomatonSaveMode.WithInput);

        using var doc = JsonDocument.Parse(automaton.ExecutionStateJson!);
        doc.RootElement.GetProperty("Input").GetString().ShouldBe("110");
    }

    // ── WithState mode verification ────────────────────────────────────────────

    [Fact]
    public async Task SavedAutomaton_WithState_ThreeAutomatonsExist()
    {
        using var db = GetDb();
        var withState = await db.SavedAutomatons
            .Where(a => a.SaveMode == AutomatonSaveMode.WithState)
            .ToListAsync();

        withState.Count.ShouldBeGreaterThanOrEqualTo(3, "Expected at least 3 WithState automatons");
    }

    [Theory]
    [InlineData("DFA – Even number of a's", "aab", 3, 0, true)]
    [InlineData("DFA – Ends in 'b'", "abb", 3, 1, true)]
    [InlineData("DFA – No two consecutive a's", "abab", 4, 0, true)]
    public async Task SavedAutomaton_WithState_HasCorrectExecutionState(
        string name, string expectedInput, int expectedPosition, int expectedStateId, bool expectedAccepted)
    {
        using var db = GetDb();
        var automaton = await db.SavedAutomatons.FirstOrDefaultAsync(a => a.Name == name);

        automaton.ShouldNotBeNull($"'{name}' not found in DB");
        automaton!.SaveMode.ShouldBe(AutomatonSaveMode.WithState, $"'{name}' should be WithState");
        automaton.ExecutionStateJson.ShouldNotBeNullOrWhiteSpace();

        using var doc = JsonDocument.Parse(automaton.ExecutionStateJson!);
        var root = doc.RootElement;
        root.GetProperty("Input").GetString().ShouldBe(expectedInput);
        root.GetProperty("Position").GetInt32().ShouldBe(expectedPosition);
        root.GetProperty("CurrentStateId").GetInt32().ShouldBe(expectedStateId);
        root.GetProperty("IsAccepted").GetBoolean().ShouldBe(expectedAccepted);
    }

    [Fact]
    public async Task SavedAutomaton_WithState_CanBeRestoredToModel()
    {
        using var db = GetDb();
        // DFA Even As saved mid-execution (after "aab" → accepted)
        var automaton = await db.SavedAutomatons
            .FirstOrDefaultAsync(a => a.Name == "DFA – Even number of a's");
        automaton.ShouldNotBeNull();

        var payload = TryLoadContentJson(automaton!.ContentJson);
        payload.ShouldNotBeNull();
        var model = BuildViewModel(payload!);

        // Restore position/state from ExecutionStateJson
        var exec = JsonSerializer.Deserialize<SavedExecutionStateDto>(automaton.ExecutionStateJson!);
        exec.ShouldNotBeNull();
        model.Input = exec!.Input ?? string.Empty;
        model.Position = exec.Position;
        model.CurrentStateId = exec.CurrentStateId;
        model.IsAccepted = exec.IsAccepted;

        model.Input.ShouldBe("aab");
        model.Position.ShouldBe(3);
        model.CurrentStateId.ShouldBe(0);
        model.IsAccepted.ShouldBe(true);
    }

    // ── DFA execution (simulate) ────────────────────────────────────────────────

    [Fact]
    public async Task DfaEvenAs_ExecuteAll_AcceptsEmptyString()
    {
        using var db = GetDb();
        var automaton = await db.SavedAutomatons
            .FirstOrDefaultAsync(a => a.Name == "DFA – Even number of a's");
        automaton.ShouldNotBeNull();

        var payload = TryLoadContentJson(automaton!.ContentJson);
        payload.ShouldNotBeNull();

        var executionService = GetService<IAutomatonExecutionService>();
        var model = BuildViewModel(payload!, "");
        var result = executionService.ExecuteAll(model);

        result.IsAccepted.ShouldBe(true, "Empty string has 0 a's (even) – should be accepted");
    }

    [Fact]
    public async Task DfaEvenAs_ExecuteAll_AcceptsStringWithTwoAs()
    {
        using var db = GetDb();
        var automaton = await db.SavedAutomatons
            .FirstOrDefaultAsync(a => a.Name == "DFA – Even number of a's");
        automaton.ShouldNotBeNull();

        var payload = TryLoadContentJson(automaton!.ContentJson);
        var executionService = GetService<IAutomatonExecutionService>();
        var model = BuildViewModel(payload!, "aab");
        var result = executionService.ExecuteAll(model);

        result.IsAccepted.ShouldBe(true, "\"aab\" has 2 a's (even) – should be accepted");
    }

    [Fact]
    public async Task DfaEvenAs_ExecuteAll_RejectsStringWithOneA()
    {
        using var db = GetDb();
        var automaton = await db.SavedAutomatons
            .FirstOrDefaultAsync(a => a.Name == "DFA – Even number of a's");
        automaton.ShouldNotBeNull();

        var payload = TryLoadContentJson(automaton!.ContentJson);
        var executionService = GetService<IAutomatonExecutionService>();
        var model = BuildViewModel(payload!, "ab");
        var result = executionService.ExecuteAll(model);

        result.IsAccepted.ShouldBe(false, "\"ab\" has 1 a (odd) – should be rejected");
    }

    [Fact]
    public async Task DfaAcceptAll_ExecuteAll_AlwaysAccepts()
    {
        using var db = GetDb();
        var automaton = await db.SavedAutomatons
            .FirstOrDefaultAsync(a => a.Name == "DFA – Accepts all strings");
        automaton.ShouldNotBeNull();

        var payload = TryLoadContentJson(automaton!.ContentJson);
        var executionService = GetService<IAutomatonExecutionService>();

        foreach (var input in new[] { "", "a", "b", "ab", "ababab" })
        {
            var model = BuildViewModel(payload!, input);
            var result = executionService.ExecuteAll(model);
            result.IsAccepted.ShouldBe(true, $"DFA accepts-all should accept \"{input}\"");
        }
    }

    [Fact]
    public async Task DfaBinaryDiv3_ExecuteAll_Accepts110()
    {
        using var db = GetDb();
        var automaton = await db.SavedAutomatons
            .FirstOrDefaultAsync(a => a.Name == "DFA – Binary numbers divisible by 3");
        automaton.ShouldNotBeNull();

        var payload = TryLoadContentJson(automaton!.ContentJson);
        var executionService = GetService<IAutomatonExecutionService>();

        var model = BuildViewModel(payload!, "110");
        var result = executionService.ExecuteAll(model);
        result.IsAccepted.ShouldBe(true, "110 in binary = 6, divisible by 3");
    }

    [Fact]
    public async Task DfaEvenLength_ExecuteAll_AcceptsEvenLengthStrings()
    {
        using var db = GetDb();
        var automaton = await db.SavedAutomatons
            .FirstOrDefaultAsync(a => a.Name == "DFA – Even-length strings");
        automaton.ShouldNotBeNull();

        var payload = TryLoadContentJson(automaton!.ContentJson);
        var executionService = GetService<IAutomatonExecutionService>();

        var accepted = new[] { "", "ab", "abab", "aabb" };
        var rejected = new[] { "a", "b", "aba", "ababa" };

        foreach (var input in accepted)
        {
            var model = BuildViewModel(payload!, input);
            executionService.ExecuteAll(model).IsAccepted.ShouldBe(true, $"\"{input}\" has even length – should be accepted");
        }
        foreach (var input in rejected)
        {
            var model = BuildViewModel(payload!, input);
            executionService.ExecuteAll(model).IsAccepted.ShouldBe(false, $"\"{input}\" has odd length – should be rejected");
        }
    }

    [Fact]
    public async Task DfaAlternating_ExecuteAll_AcceptsAndRejectsCorrectly()
    {
        using var db = GetDb();
        var automaton = await db.SavedAutomatons
            .FirstOrDefaultAsync(a => a.Name == "DFA – Alternating a and b");
        automaton.ShouldNotBeNull();

        var payload = TryLoadContentJson(automaton!.ContentJson);
        var executionService = GetService<IAutomatonExecutionService>();

        var accepted = new[] { "a", "ab", "aba", "abab", "ababab" };
        var rejected = new[] { "", "b", "aa", "ba", "bb", "abb" };

        foreach (var input in accepted)
        {
            var model = BuildViewModel(payload!, input);
            executionService.ExecuteAll(model).IsAccepted.ShouldBe(true, $"\"{input}\" should be accepted by alternating DFA");
        }
        foreach (var input in rejected)
        {
            var model = BuildViewModel(payload!, input);
            executionService.ExecuteAll(model).IsAccepted.ShouldBe(false, $"\"{input}\" should be rejected by alternating DFA");
        }
    }

    // ── Step-by-step execution ─────────────────────────────────────────────────

    [Fact]
    public async Task DfaEvenAs_StepForward_AdvancesPosition()
    {
        using var db = GetDb();
        var automaton = await db.SavedAutomatons
            .FirstOrDefaultAsync(a => a.Name == "DFA – Even number of a's");
        automaton.ShouldNotBeNull();

        var payload = TryLoadContentJson(automaton!.ContentJson);
        var executionService = GetService<IAutomatonExecutionService>();
        var model = BuildViewModel(payload!, "ab");

        model = executionService.ExecuteStepForward(model);
        model.Position.ShouldBe(1);
        model.IsAccepted.ShouldBeNull("Execution not complete yet");

        model = executionService.ExecuteStepForward(model);
        model.Position.ShouldBe(2);
        // "ab" has 1 'a' which is odd → DFA Even As rejects it
        model.IsAccepted.ShouldBe(false, "\"ab\" has 1 a (odd) – should be rejected");
    }

    // ── NFA execution ──────────────────────────────────────────────────────────

    [Fact]
    public async Task NfaContainsAb_ExecuteAll_AcceptsStringsContainingAb()
    {
        using var db = GetDb();
        var automaton = await db.SavedAutomatons
            .FirstOrDefaultAsync(a => a.Name == "NFA – Contains substring 'ab'");
        automaton.ShouldNotBeNull();

        var payload = TryLoadContentJson(automaton!.ContentJson);
        var executionService = GetService<IAutomatonExecutionService>();

        foreach (var input in new[] { "ab", "aab", "bab", "abb", "abab" })
        {
            var model = BuildViewModel(payload!, input);
            executionService.ExecuteAll(model).IsAccepted.ShouldBe(true, $"\"{input}\" contains 'ab'");
        }

        foreach (var input in new[] { "a", "b", "ba", "bba" })
        {
            var model = BuildViewModel(payload!, input);
            executionService.ExecuteAll(model).IsAccepted.ShouldBe(false, $"\"{input}\" does not contain 'ab'");
        }
    }

    // ── DFA minimization ──────────────────────────────────────────────────────

    [Fact]
    public async Task DfaStartsWithAMinimizable_Minimizes_ReducesStateCount()
    {
        using var db = GetDb();
        var automaton = await db.SavedAutomatons
            .FirstOrDefaultAsync(a => a.Name == "DFA – Starts with 'a' (minimizable)");
        automaton.ShouldNotBeNull();

        var payload = TryLoadContentJson(automaton!.ContentJson);
        var minimizationService = GetService<IAutomatonMinimizationService>();
        var model = BuildViewModel(payload!);

        model.States.Count.ShouldBe(4, "Original minimizable DFA has 4 states");

        var (minimized, message) = minimizationService.MinimizeDfa(model);

        minimized.States.Count.ShouldBeLessThan(4, "Minimized DFA should have fewer states");
        minimized.Type.ShouldBe(AutomatonType.DFA);
        message.ShouldContain("minimized", Case.Insensitive);
    }

    [Fact]
    public async Task DfaAlreadyMinimal_Minimize_ReturnsOriginalCount()
    {
        using var db = GetDb();
        var automaton = await db.SavedAutomatons
            .FirstOrDefaultAsync(a => a.Name == "DFA – Even number of a's");
        automaton.ShouldNotBeNull();

        var payload = TryLoadContentJson(automaton!.ContentJson);
        var minimizationService = GetService<IAutomatonMinimizationService>();
        var model = BuildViewModel(payload!);

        var (minimized, message) = minimizationService.MinimizeDfa(model);

        minimized.States.Count.ShouldBe(model.States.Count, "Already minimal DFA – state count unchanged");
        message.ShouldContain("minimal", Case.Insensitive);
    }

    // ── NFA → DFA conversion ──────────────────────────────────────────────────

    [Fact]
    public async Task NfaContainsAb_ConvertToDfa_ProducesDfa()
    {
        using var db = GetDb();
        var automaton = await db.SavedAutomatons
            .FirstOrDefaultAsync(a => a.Name == "NFA – Contains substring 'ab'");
        automaton.ShouldNotBeNull();

        var payload = TryLoadContentJson(automaton!.ContentJson);
        var conversionService = GetService<IAutomatonConversionService>();
        var model = BuildViewModel(payload!);

        var dfa = conversionService.ConvertToDFA(model);

        dfa.Type.ShouldBe(AutomatonType.DFA);
        dfa.States.Count.ShouldBeGreaterThan(0);
        dfa.Transitions.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task NfaContainsAb_ConvertedDfa_ExecutesCorrectly()
    {
        using var db = GetDb();
        var automaton = await db.SavedAutomatons
            .FirstOrDefaultAsync(a => a.Name == "NFA – Contains substring 'ab'");
        automaton.ShouldNotBeNull();

        var payload = TryLoadContentJson(automaton!.ContentJson);
        var conversionService = GetService<IAutomatonConversionService>();
        var executionService = GetService<IAutomatonExecutionService>();

        var nfaModel = BuildViewModel(payload!);
        var dfa = conversionService.ConvertToDFA(nfaModel);

        // DFA must accept same strings as NFA
        foreach (var input in new[] { "ab", "bab", "abab" })
        {
            var model = BuildViewModel(new AutomatonPayloadDto { Type = dfa.Type, States = dfa.States, Transitions = dfa.Transitions }, input);
            executionService.ExecuteAll(model).IsAccepted.ShouldBe(true, $"Converted DFA should accept \"{input}\"");
        }

        foreach (var input in new[] { "a", "b", "ba", "bba" })
        {
            var model = BuildViewModel(new AutomatonPayloadDto { Type = dfa.Type, States = dfa.States, Transitions = dfa.Transitions }, input);
            executionService.ExecuteAll(model).IsAccepted.ShouldBe(false, $"Converted DFA should reject \"{input}\"");
        }
    }

    // ── ε-NFA execution ────────────────────────────────────────────────────────

    [Fact]
    public async Task EnfaABorC_ExecuteAll_AcceptsAbAndAc()
    {
        using var db = GetDb();
        var automaton = await db.SavedAutomatons
            .FirstOrDefaultAsync(a => a.Name == "ε-NFA – a(b|c)");
        automaton.ShouldNotBeNull();

        var payload = TryLoadContentJson(automaton!.ContentJson);
        var executionService = GetService<IAutomatonExecutionService>();

        foreach (var input in new[] { "ab", "ac" })
        {
            var model = BuildViewModel(payload!, input);
            executionService.ExecuteAll(model).IsAccepted.ShouldBe(true, $"ε-NFA a(b|c) should accept \"{input}\"");
        }

        foreach (var input in new[] { "a", "b", "c", "abc", "aa" })
        {
            var model = BuildViewModel(payload!, input);
            executionService.ExecuteAll(model).IsAccepted.ShouldBe(false, $"ε-NFA a(b|c) should reject \"{input}\"");
        }
    }

    // ── DPDA execution ─────────────────────────────────────────────────────────

    [Fact]
    public async Task DpdaAnBn_ExecuteAll_AcceptsAndBn()
    {
        using var db = GetDb();
        var automaton = await db.SavedAutomatons
            .FirstOrDefaultAsync(a => a.Name == "DPDA – aⁿbⁿ (n ≥ 0)");
        automaton.ShouldNotBeNull();

        var payload = TryLoadContentJson(automaton!.ContentJson);
        var executionService = GetService<IAutomatonExecutionService>();

        foreach (var input in new[] { "", "ab", "aabb", "aaabbb" })
        {
            var model = BuildViewModel(payload!, input);
            executionService.ExecuteAll(model).IsAccepted.ShouldBe(true, $"DPDA aⁿbⁿ should accept \"{input}\"");
        }

        foreach (var input in new[] { "a", "b", "aab", "abb", "ba" })
        {
            var model = BuildViewModel(payload!, input);
            executionService.ExecuteAll(model).IsAccepted.ShouldBe(false, $"DPDA aⁿbⁿ should reject \"{input}\"");
        }
    }

    [Fact]
    public async Task DpdaBalancedParens_ExecuteAll_AcceptsBalancedStrings()
    {
        using var db = GetDb();
        var automaton = await db.SavedAutomatons
            .FirstOrDefaultAsync(a => a.Name == "DPDA – Balanced parentheses");
        automaton.ShouldNotBeNull();

        var payload = TryLoadContentJson(automaton!.ContentJson);
        var executionService = GetService<IAutomatonExecutionService>();

        foreach (var input in new[] { "", "()", "(())", "()()" })
        {
            var model = BuildViewModel(payload!, input);
            executionService.ExecuteAll(model).IsAccepted.ShouldBe(true, $"Balanced parens DPDA should accept \"{input}\"");
        }

        foreach (var input in new[] { "(", ")", "(()", "(()(" })
        {
            var model = BuildViewModel(payload!, input);
            executionService.ExecuteAll(model).IsAccepted.ShouldBe(false, $"Balanced parens DPDA should reject \"{input}\"");
        }
    }

    // ── Seeder idempotency ─────────────────────────────────────────────────────

    [Fact]
    public async Task SeedAsync_CalledTwice_DoesNotDuplicateData()
    {
        using var db = GetDb();
        var countBefore = await db.SavedAutomatons.CountAsync();

        // Re-run seeder; it should detect supervisor exists and skip
        var scope = GetServiceScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DemoDataSeeder>();
        await seeder.SeedAsync();

        countBefore.ShouldBe(await db.SavedAutomatons.CountAsync(),
            "Re-running the seeder must not create duplicate automatons");
    }
}
