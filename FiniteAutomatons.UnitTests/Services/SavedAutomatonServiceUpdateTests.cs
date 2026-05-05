using FiniteAutomatons.Core.Models.Database;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Data;
using FiniteAutomatons.Services.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using System.Text.Json;

namespace FiniteAutomatons.UnitTests.Services;

public class SavedAutomatonServiceUpdateTests
{
    private static ApplicationDbContext CreateInMemoryDb(string name)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: name)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static AutomatonViewModel BuildDfaModel(string input = "") => new()
    {
        Type = AutomatonType.DFA,
        States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
        Transitions = [],
        Input = input
    };

    private static async Task<SavedAutomaton> SeedOneAsync(SavedAutomatonService svc, string userId = "owner", string name = "Original")
        => await svc.SaveAsync(userId, name, "Original Desc", BuildDfaModel());

    // ── UpdateAsync happy path ───────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_ChangesNameAndDescription()
    {
        using var db = CreateInMemoryDb("update_name_desc");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);
        var saved = await SeedOneAsync(svc);

        var model = BuildDfaModel();
        var result = await svc.UpdateAsync(saved.Id, "owner", "New Name", "New Desc", model);

        result.Name.ShouldBe("New Name");
        result.Description.ShouldBe("New Desc");
    }

    [Fact]
    public async Task UpdateAsync_PersistsChangesToDatabase()
    {
        using var db = CreateInMemoryDb("update_persists");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);
        var saved = await SeedOneAsync(svc);

        await svc.UpdateAsync(saved.Id, "owner", "Persisted Name", null, BuildDfaModel());

        var fromDb = await db.SavedAutomatons.FindAsync(saved.Id);
        fromDb.ShouldNotBeNull();
        fromDb!.Name.ShouldBe("Persisted Name");
        fromDb.Description.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_UpdatesContentJson_WithNewStructure()
    {
        using var db = CreateInMemoryDb("update_content");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);
        var saved = await SeedOneAsync(svc);

        var updatedModel = new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true }],
            Transitions = [new() { FromStateId = 1, ToStateId = 2, Symbol = 'b' }]
        };

        var result = await svc.UpdateAsync(saved.Id, "owner", "Updated", null, updatedModel);

        var doc = JsonDocument.Parse(result.ContentJson);
        var typeValue = doc.RootElement.GetProperty("Type");
        // AutomatonType is serialized as its integer value
        typeValue.GetInt32().ShouldBe((int)AutomatonType.NFA);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesLayoutAndThumbnail()
    {
        using var db = CreateInMemoryDb("update_layout_thumb");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);
        var saved = await SeedOneAsync(svc);

        var newLayout = "[{\"id\":\"1\",\"position\":{\"x\":50,\"y\":50}}]";
        var newThumb = "data:image/png;base64,NEW==";

        var result = await svc.UpdateAsync(saved.Id, "owner", "N", null, BuildDfaModel(),
            layoutJson: newLayout, thumbnailBase64: newThumb);

        result.LayoutJson.ShouldBe(newLayout);
        result.ThumbnailBase64.ShouldBe(newThumb);
    }

    [Fact]
    public async Task UpdateAsync_WhitespaceLayout_StoresNull()
    {
        using var db = CreateInMemoryDb("update_whitespace_layout");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);
        var saved = await SeedOneAsync(svc);

        var result = await svc.UpdateAsync(saved.Id, "owner", "N", null, BuildDfaModel(),
            layoutJson: "   ", thumbnailBase64: "   ");

        result.LayoutJson.ShouldBeNull();
        result.ThumbnailBase64.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_WithExecutionState_SetsSaveModeWithState()
    {
        using var db = CreateInMemoryDb("update_with_state");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);
        var saved = await SeedOneAsync(svc);

        var model = BuildDfaModel("ab");
        model.HasExecuted = true;
        model.Position = 2;
        model.CurrentStateId = 1;
        model.IsAccepted = true;

        var result = await svc.UpdateAsync(saved.Id, "owner", "N", null, model, saveExecutionState: true);

        result.SaveMode.ShouldBe(AutomatonSaveMode.WithState);
        result.ExecutionStateJson.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UpdateAsync_WithInputOnly_SetsSaveModeWithInput()
    {
        using var db = CreateInMemoryDb("update_with_input");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);
        var saved = await SeedOneAsync(svc);

        var model = BuildDfaModel("hello");

        var result = await svc.UpdateAsync(saved.Id, "owner", "N", null, model, saveExecutionState: false);

        result.SaveMode.ShouldBe(AutomatonSaveMode.WithInput);
    }

    [Fact]
    public async Task UpdateAsync_NoInput_SetsSaveModeStructure()
    {
        using var db = CreateInMemoryDb("update_structure");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);
        var saved = await SeedOneAsync(svc);

        var result = await svc.UpdateAsync(saved.Id, "owner", "N", null, BuildDfaModel(), saveExecutionState: false);

        result.SaveMode.ShouldBe(AutomatonSaveMode.Structure);
        result.ExecutionStateJson.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_PreservesGroupAssignment()
    {
        using var db = CreateInMemoryDb("update_preserves_group");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);

        // Create group and save automaton into it
        var group = await svc.CreateGroupAsync("owner", "Group", null);
        var saved = await svc.SaveAsync("owner", "Original", null, BuildDfaModel(), groupId: group.Id);

        // Update (no groupId param — group assignment comes from existing record)
        await svc.UpdateAsync(saved.Id, "owner", "Updated", null, BuildDfaModel());

        // Group assignment should still exist
        var assignments = await db.SavedAutomatonGroupAssignments
            .Where(a => a.AutomatonId == saved.Id)
            .ToListAsync();
        assignments.ShouldNotBeEmpty();
    }

    // ── UpdateAsync error cases ──────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_WrongOwner_ThrowsInvalidOperation()
    {
        using var db = CreateInMemoryDb("update_wrong_owner");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);
        var saved = await SeedOneAsync(svc, userId: "alice");

        await Should.ThrowAsync<InvalidOperationException>(
            () => svc.UpdateAsync(saved.Id, "bob", "Hack", null, BuildDfaModel()));
    }

    [Fact]
    public async Task UpdateAsync_NonExistentId_ThrowsInvalidOperation()
    {
        using var db = CreateInMemoryDb("update_not_found");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);

        await Should.ThrowAsync<InvalidOperationException>(
            () => svc.UpdateAsync(9999, "owner", "Name", null, BuildDfaModel()));
    }

    [Fact]
    public async Task UpdateAsync_NullUserId_ThrowsArgumentNullException()
    {
        using var db = CreateInMemoryDb("update_null_user");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);

        await Should.ThrowAsync<ArgumentNullException>(
            () => svc.UpdateAsync(1, null!, "Name", null, BuildDfaModel()));
    }

    [Fact]
    public async Task UpdateAsync_NullName_ThrowsArgumentNullException()
    {
        using var db = CreateInMemoryDb("update_null_name");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);
        var saved = await SeedOneAsync(svc);

        await Should.ThrowAsync<ArgumentNullException>(
            () => svc.UpdateAsync(saved.Id, "owner", null!, null, BuildDfaModel()));
    }

    [Fact]
    public async Task UpdateAsync_NullModel_ThrowsArgumentNullException()
    {
        using var db = CreateInMemoryDb("update_null_model");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);
        var saved = await SeedOneAsync(svc);

        await Should.ThrowAsync<ArgumentNullException>(
            () => svc.UpdateAsync(saved.Id, "owner", "N", null, null!));
    }

    // ── UpdateAsync does NOT affect other records ────────────────────────────

    [Fact]
    public async Task UpdateAsync_DoesNotModifyOtherAutomatons()
    {
        using var db = CreateInMemoryDb("update_isolation");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);
        var first = await SeedOneAsync(svc, name: "First");
        var second = await SeedOneAsync(svc, name: "Second");

        await svc.UpdateAsync(first.Id, "owner", "First Updated", null, BuildDfaModel());

        var secondFromDb = await db.SavedAutomatons.FindAsync(second.Id);
        secondFromDb!.Name.ShouldBe("Second");
    }

    // ── Round-trip: Save then Update and verify via Get ──────────────────────

    [Fact]
    public async Task UpdateAsync_RoundTrip_GetReturnsUpdatedValues()
    {
        using var db = CreateInMemoryDb("update_roundtrip");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);
        var saved = await SeedOneAsync(svc, name: "Before Update");

        await svc.UpdateAsync(saved.Id, "owner", "After Update", "New description", BuildDfaModel());

        var retrieved = await svc.GetAsync(saved.Id, "owner");
        retrieved.ShouldNotBeNull();
        retrieved!.Name.ShouldBe("After Update");
        retrieved.Description.ShouldBe("New description");
    }
}
