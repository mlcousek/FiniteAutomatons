using FiniteAutomatons.Core.Models.Database;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace FiniteAutomatons.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
        try
        {
            if (Database.GetService<IDatabaseCreator>() is RelationalDatabaseCreator databaseCreator)
            {
                if (!databaseCreator.CanConnect()) databaseCreator.Create();
                if (!databaseCreator.HasTables()) databaseCreator.CreateTables();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    public DbSet<SavedAutomaton> SavedAutomatons { get; set; } = null!;
    public DbSet<SavedAutomatonGroup> SavedAutomatonGroups { get; set; } = null!;
    public DbSet<SavedAutomatonGroupMember> SavedAutomatonGroupMembers { get; set; } = null!;
    public DbSet<SavedAutomatonGroupAssignment> SavedAutomatonGroupAssignments { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SavedAutomatonGroup>(b =>
        {
            b.HasKey(g => g.Id);

            b.Property(g => g.UserId).IsRequired();
            b.Property(g => g.Name).IsRequired().HasMaxLength(200);
            b.Property(g => g.MembersCanShare).HasDefaultValue(true);

            b.HasMany(g => g.Members)
             .WithOne(m => m.Group)
             .HasForeignKey(m => m.GroupId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SavedAutomatonGroupAssignment>(b =>
        {
            b.HasKey(a => a.Id);
            b.HasOne(a => a.Group)
             .WithMany(g => g.Assignments)
             .HasForeignKey(a => a.GroupId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(a => a.Automaton)
             .WithMany(a => a.Assignments)
             .HasForeignKey(a => a.AutomatonId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SavedAutomatonGroupMember>(b =>
        {
            b.HasKey(m => m.Id);
            b.Property(m => m.UserId).IsRequired();
        });

        modelBuilder.Entity<SavedAutomaton>(b =>
        {
            b.HasKey(a => a.Id);
            b.Property(a => a.UserId).IsRequired();
            b.Property(a => a.Name).IsRequired().HasMaxLength(200);
            b.Property(a => a.ContentJson).IsRequired();
            b.Property(a => a.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        });
    }
}
