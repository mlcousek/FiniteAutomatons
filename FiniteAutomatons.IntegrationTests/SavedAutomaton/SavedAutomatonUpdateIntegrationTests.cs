using FiniteAutomatons.Core.Models.Database;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace FiniteAutomatons.IntegrationTests.SavedAutomaton;

/// <summary>
/// Integration tests for the "update existing vs. save as new" feature.
/// All tests operate through the real <see cref="ISavedAutomatonService"/> wired to
/// the SQL Server test database so that EF Core, migrations, and constraints are exercised.
/// </summary>
[Collection("Integration Tests")]
public class SavedAutomatonUpdateIntegrationTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    private const string OwnerId = "alice-id";

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens a DI scope and resolves the saved automaton service within it.
    /// Callers must dispose the returned scope: <c>using var scope = OpenScope(out var svc);</c>
    /// </summary>
    private IServiceScope OpenScope(out ISavedAutomatonService svc)
    {
        var scope = GetServiceScope();
        svc = scope.ServiceProvider.GetRequiredService<ISavedAutomatonService>();
        return scope;
    }

    private static AutomatonViewModel BuildDfaModel(string input = "") => new()
    {
        Type = AutomatonType.DFA,
        States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
        Transitions = [],
        Input = input,
        IsCustomAutomaton = true
    };

    private static string UniqueUserId() => $"test-user-{Guid.NewGuid():N}";

    // ── UpdateAsync: basic persistence ────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_ChangesNameAndDescription_PersistedToDatabase()
    {
        using var scope = OpenScope(out var svc);
        var userId = UniqueUserId();
        var saved = await svc.SaveAsync(userId, "Original Name", "Original Desc", BuildDfaModel());

        await svc.UpdateAsync(saved.Id, userId, "Updated Name", "Updated Desc", BuildDfaModel());

        var fromDb = await svc.GetAsync(saved.Id, userId);
        fromDb.ShouldNotBeNull();
        fromDb!.Name.ShouldBe("Updated Name");
        fromDb.Description.ShouldBe("Updated Desc");
    }

    [Fact]
    public async Task UpdateAsync_UpdatesContentJson_WithNewAutomatonStructure()
    {
        using var scope = OpenScope(out var svc);
        var userId = UniqueUserId();
        var saved = await svc.SaveAsync(userId, "My DFA", null, BuildDfaModel());

        var nfaModel = new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true }],
            Transitions = [new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }],
            IsCustomAutomaton = true
        };

        await svc.UpdateAsync(saved.Id, userId, "My NFA", null, nfaModel);

        var updated = await svc.GetAsync(saved.Id, userId);
        updated.ShouldNotBeNull();
        // AutomatonType is serialized as an integer (NFA = 1)
        var doc = System.Text.Json.JsonDocument.Parse(updated!.ContentJson);
        doc.RootElement.GetProperty("Type").GetInt32().ShouldBe((int)AutomatonType.NFA);
    }

    [Fact]
    public async Task UpdateAsync_UpgradesFromStructure_ToWithInputSaveMode()
    {
        using var scope = OpenScope(out var svc);
        var userId = UniqueUserId();
        var saved = await svc.SaveAsync(userId, "Struct", null, BuildDfaModel());
        saved.SaveMode.ShouldBe(AutomatonSaveMode.Structure);

        await svc.UpdateAsync(saved.Id, userId, "With Input", null, BuildDfaModel("abc"), saveExecutionState: false);

        var updated = await svc.GetAsync(saved.Id, userId);
        updated!.SaveMode.ShouldBe(AutomatonSaveMode.WithInput);
        updated.ExecutionStateJson.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UpdateAsync_UpgradesFromStructure_ToWithStateSaveMode()
    {
        using var scope = OpenScope(out var svc);
        var userId = UniqueUserId();
        var saved = await svc.SaveAsync(userId, "Struct", null, BuildDfaModel());

        var model = BuildDfaModel("ab");
        model.HasExecuted = true;
        model.Position = 2;
        model.CurrentStateId = 1;
        model.IsAccepted = true;

        await svc.UpdateAsync(saved.Id, userId, "With State", null, model, saveExecutionState: true);

        var updated = await svc.GetAsync(saved.Id, userId);
        updated!.SaveMode.ShouldBe(AutomatonSaveMode.WithState);
        updated.ExecutionStateJson.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UpdateAsync_DowngradesFromWithState_ToStructure()
    {
        using var scope = OpenScope(out var svc);
        var userId = UniqueUserId();
        var model = BuildDfaModel("ab");
        model.HasExecuted = true;
        model.Position = 2;
        model.CurrentStateId = 1;
        model.IsAccepted = true;

        var saved = await svc.SaveAsync(userId, "With State", null, model, saveExecutionState: true);
        saved.SaveMode.ShouldBe(AutomatonSaveMode.WithState);

        // Update with no input → should become Structure
        await svc.UpdateAsync(saved.Id, userId, "Now Structure", null, BuildDfaModel(), saveExecutionState: false);

        var updated = await svc.GetAsync(saved.Id, userId);
        updated!.SaveMode.ShouldBe(AutomatonSaveMode.Structure);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesLayoutAndThumbnail()
    {
        using var scope = OpenScope(out var svc);
        var userId = UniqueUserId();
        var saved = await svc.SaveAsync(userId, "Automaton", null, BuildDfaModel(),
            layoutJson: "[{\"id\":\"1\",\"position\":{\"x\":0,\"y\":0}}]",
            thumbnailBase64: "old_thumb");

        var newLayout = "[{\"id\":\"1\",\"position\":{\"x\":100,\"y\":200}}]";
        var newThumb = "new_thumb_base64";

        await svc.UpdateAsync(saved.Id, userId, "Automaton", null, BuildDfaModel(),
            layoutJson: newLayout, thumbnailBase64: newThumb);

        var updated = await svc.GetAsync(saved.Id, userId);
        updated!.LayoutJson.ShouldBe(newLayout);
        updated.ThumbnailBase64.ShouldBe(newThumb);
    }

    [Fact]
    public async Task UpdateAsync_PreservesCreatedAtTimestamp()
    {
        using var scope = OpenScope(out var svc);
        var userId = UniqueUserId();
        var saved = await svc.SaveAsync(userId, "Automaton", null, BuildDfaModel());
        var originalCreatedAt = saved.CreatedAt;

        await Task.Delay(50); // ensure clock advances
        await svc.UpdateAsync(saved.Id, userId, "Updated", null, BuildDfaModel());

        var updated = await svc.GetAsync(saved.Id, userId);
        updated!.CreatedAt.ShouldBe(originalCreatedAt);
    }

    // ── UpdateAsync: access control ───────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_DifferentUser_ThrowsInvalidOperation()
    {
        using var scope = OpenScope(out var svc);
        var userId = UniqueUserId();
        var saved = await svc.SaveAsync(userId, "Private", null, BuildDfaModel());

        await Should.ThrowAsync<InvalidOperationException>(
            () => svc.UpdateAsync(saved.Id, UniqueUserId(), "Hacked", null, BuildDfaModel()));
    }

    [Fact]
    public async Task UpdateAsync_NonExistentId_ThrowsInvalidOperation()
    {
        using var scope = OpenScope(out var svc);

        await Should.ThrowAsync<InvalidOperationException>(
            () => svc.UpdateAsync(int.MaxValue, UniqueUserId(), "Ghost", null, BuildDfaModel()));
    }

    // ── Save as new: independent records ─────────────────────────────────────

    [Fact]
    public async Task SaveAsync_After_Existing_CreatesIndependentRecord()
    {
        using var scope = OpenScope(out var svc);
        var userId = UniqueUserId();
        var original = await svc.SaveAsync(userId, "Original", null, BuildDfaModel());

        // Simulate "Save as new" — user picks a new name but same content
        var copy = await svc.SaveAsync(userId, "Copy", "Based on original", BuildDfaModel());

        copy.Id.ShouldNotBe(original.Id);

        var list = await svc.ListForUserAsync(userId);
        list.Count.ShouldBe(2);
        list.ShouldContain(a => a.Name == "Original");
        list.ShouldContain(a => a.Name == "Copy");
    }

    [Fact]
    public async Task SaveAsync_DoesNotModifyOriginal_WhenSavingNewCopy()
    {
        using var scope = OpenScope(out var svc);
        var userId = UniqueUserId();
        var original = await svc.SaveAsync(userId, "Original", null, BuildDfaModel());

        // Save a new copy with different structure
        var nfaModel = new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            IsCustomAutomaton = true
        };
        await svc.SaveAsync(userId, "Copy", null, nfaModel);

        // Original must be untouched — AutomatonType.DFA = 0
        var refreshed = await svc.GetAsync(original.Id, userId);
        refreshed.ShouldNotBeNull();
        refreshed!.Name.ShouldBe("Original");
        var doc = System.Text.Json.JsonDocument.Parse(refreshed.ContentJson);
        doc.RootElement.GetProperty("Type").GetInt32().ShouldBe((int)AutomatonType.DFA);
    }

    // ── UpdateAsync: does not affect other users' records ────────────────────

    [Fact]
    public async Task UpdateAsync_DoesNotAffectOtherUsersAutomatons()
    {
        using var scope = OpenScope(out var svc);
        var aliceId = UniqueUserId();
        var bobId = UniqueUserId();

        var aliceRecord = await svc.SaveAsync(aliceId, "Alice's DFA", null, BuildDfaModel());
        var bobRecord = await svc.SaveAsync(bobId, "Bob's DFA", null, BuildDfaModel());

        await svc.UpdateAsync(aliceRecord.Id, aliceId, "Alice's Updated DFA", null, BuildDfaModel());

        var bobRefreshed = await svc.GetAsync(bobRecord.Id, bobId);
        bobRefreshed!.Name.ShouldBe("Bob's DFA");
    }

    // ── LoadedAutomatonId in the ViewModel ───────────────────────────────────

    [Fact]
    public async Task AutomatonViewModel_LoadedAutomatonId_DefaultsToNull()
    {
        var model = new AutomatonViewModel();
        model.LoadedAutomatonId.ShouldBeNull();
    }

    [Fact]
    public async Task AutomatonViewModel_LoadedAutomatonId_CanBeSetAndRead()
    {
        var model = new AutomatonViewModel { LoadedAutomatonId = 42 };
        model.LoadedAutomatonId.ShouldBe(42);
    }
}
