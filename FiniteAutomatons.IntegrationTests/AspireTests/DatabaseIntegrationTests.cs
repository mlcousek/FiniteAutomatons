using Aspire.Hosting.Testing;
using FiniteAutomatons.Core.Models.Database;
using FiniteAutomatons.Data;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace FiniteAutomatons.IntegrationTests.AspireTests;

public class AspireTestFixture : IAsyncLifetime
{
    public Aspire.Hosting.DistributedApplication? App { get; private set; }
    public HttpClient? HttpClient { get; private set; }
    public string? ConnectionString { get; private set; }

    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.FiniteAutomatons_AppHost>();

        App = await appHost.BuildAsync();
        await App.StartAsync();

        HttpClient = App.CreateHttpClient("finiteautomatons");
        ConnectionString = await App.GetConnectionStringAsync("finiteautomatonsdb");

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(ConnectionString);
        await using var dbContext = new ApplicationDbContext(optionsBuilder.Options);
        await dbContext.Database.EnsureCreatedAsync();

        await Task.Delay(1000);
    }

    public async Task DisposeAsync()
    {
        HttpClient?.Dispose();

        if (App != null)
        {
            await App.DisposeAsync();
        }
    }
}

public class DatabaseIntegrationTests(AspireTestFixture fixture) : IClassFixture<AspireTestFixture>
{
    private readonly AspireTestFixture fixture = fixture;

    private ApplicationDbContext CreateDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(fixture.ConnectionString);
        return new ApplicationDbContext(optionsBuilder.Options);
    }

    [Fact]
    public async Task CanSaveAutomatonToDatabase()
    {
        await using var dbContext = CreateDbContext();

        var userId = Guid.NewGuid().ToString();

        var savedAutomaton = new SavedAutomaton
        {
            Name = $"Test DFA {Guid.NewGuid():N}",
            ContentJson = "{\"states\":[],\"transitions\":[]}",
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            SaveMode = AutomatonSaveMode.Structure
        };

        dbContext.SavedAutomatons.Add(savedAutomaton);
        await dbContext.SaveChangesAsync();

        var retrieved = await dbContext.SavedAutomatons
            .FirstOrDefaultAsync(a => a.Name == savedAutomaton.Name);

        retrieved.ShouldNotBeNull();
    }

    [Fact]
    public async Task CanCreateSharedAutomaton()
    {
        await using var dbContext = CreateDbContext();

        var userId = Guid.NewGuid().ToString();

        var sharedAutomaton = new SharedAutomaton
        {
            Name = $"Shared Test DFA {Guid.NewGuid():N}",
            ContentJson = "{\"states\":[],\"transitions\":[]}",
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.SharedAutomatons.Add(sharedAutomaton);
        await dbContext.SaveChangesAsync();

        var retrieved = await dbContext.SharedAutomatons
            .FirstOrDefaultAsync(a => a.Name == sharedAutomaton.Name);

        retrieved.ShouldNotBeNull();
        retrieved.CreatedByUserId.ShouldBe(userId);
    }

    [Fact]
    public async Task CanCreateSavedAutomatonGroup()
    {
        await using var dbContext = CreateDbContext();

        var userId = Guid.NewGuid().ToString();

        var group = new SavedAutomatonGroup
        {
            Name = $"Test Group {Guid.NewGuid():N}",
            UserId = userId
        };

        dbContext.SavedAutomatonGroups.Add(group);
        await dbContext.SaveChangesAsync();

        var retrieved = await dbContext.SavedAutomatonGroups
            .FirstOrDefaultAsync(g => g.Name == group.Name);

        retrieved.ShouldNotBeNull();
        retrieved.UserId.ShouldBe(userId);
    }

    [Fact]
    public async Task CanCreateSharedAutomatonGroupWithInvitations()
    {
        await using var dbContext = CreateDbContext();

        var ownerId = Guid.NewGuid().ToString();
        var inviteeEmail = $"invitee{Guid.NewGuid():N}@test.com";

        var group = new SharedAutomatonGroup
        {
            Name = $"Collaboration Group {Guid.NewGuid():N}",
            UserId = ownerId,
            InviteCode = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow
        };

        dbContext.SharedAutomatonGroups.Add(group);
        await dbContext.SaveChangesAsync();

        var invitation = new SharedAutomatonGroupInvitation
        {
            GroupId = group.Id,
            Email = inviteeEmail,
            Role = SharedGroupRole.Contributor,
            InvitedByUserId = ownerId,
            Status = InvitationStatus.Pending,
            Token = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow
        };

        dbContext.SharedAutomatonGroupInvitations.Add(invitation);
        await dbContext.SaveChangesAsync();

        var retrievedInvitation = await dbContext.SharedAutomatonGroupInvitations
            .Include(i => i.Group)
            .FirstOrDefaultAsync(i => i.Email == inviteeEmail);

        retrievedInvitation.ShouldNotBeNull();
        retrievedInvitation.Group.ShouldNotBeNull();
        retrievedInvitation.Group.Name.ShouldBe(group.Name);
    }

    [Fact]
    public async Task DatabaseIndexesExist()
    {
        await using var dbContext = CreateDbContext();

        var indexes = await dbContext.Database
            .SqlQueryRaw<string>(@"
                SELECT CONCAT(t.name, '.', i.name) as IndexName
                FROM sys.indexes i
                INNER JOIN sys.tables t ON i.object_id = t.object_id
                WHERE i.type > 0 AND i.is_primary_key = 0")
            .ToListAsync();

        indexes.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task CanQueryAutomatonsByUser()
    {
        await using var dbContext = CreateDbContext();

        var userId = Guid.NewGuid().ToString();

        for (int i = 0; i < 5; i++)
        {
            var automaton = new SavedAutomaton
            {
                Name = $"DFA {i} {Guid.NewGuid():N}",
                ContentJson = "{}",
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                SaveMode = AutomatonSaveMode.Structure
            };
            dbContext.SavedAutomatons.Add(automaton);
        }

        await dbContext.SaveChangesAsync();

        var userAutomatons = await dbContext.SavedAutomatons
            .Where(a => a.UserId == userId)
            .ToListAsync();

        userAutomatons.Count.ShouldBe(5);
    }

    [Fact]
    public async Task CascadeDeleteWorksForGroups()
    {
        await using var dbContext = CreateDbContext();

        var userId = Guid.NewGuid().ToString();

        var group = new SavedAutomatonGroup
        {
            Name = $"To Delete {Guid.NewGuid():N}",
            UserId = userId
        };

        dbContext.SavedAutomatonGroups.Add(group);
        await dbContext.SaveChangesAsync();

        var automaton = new SavedAutomaton
        {
            Name = $"In Group {Guid.NewGuid():N}",
            ContentJson = "{}",
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            SaveMode = AutomatonSaveMode.Structure
        };
        dbContext.SavedAutomatons.Add(automaton);
        await dbContext.SaveChangesAsync();

        var assignment = new SavedAutomatonGroupAssignment
        {
            GroupId = group.Id,
            AutomatonId = automaton.Id
        };
        dbContext.SavedAutomatonGroupAssignments.Add(assignment);
        await dbContext.SaveChangesAsync();

        var assignmentId = assignment.GroupId;

        dbContext.SavedAutomatonGroups.Remove(group);
        await dbContext.SaveChangesAsync();

        var deletedAssignment = await dbContext.SavedAutomatonGroupAssignments
            .FirstOrDefaultAsync(a => a.GroupId == assignmentId);

        deletedAssignment.ShouldBeNull();
    }

    [Fact]
    public async Task DatabaseConnectionWorks()
    {
        await using var dbContext = CreateDbContext();

        var canConnect = await dbContext.Database.CanConnectAsync();

        canConnect.ShouldBeTrue();
    }
}
