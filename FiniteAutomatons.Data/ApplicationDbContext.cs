using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using FiniteAutomatons.Core.Models.Database;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SavedAutomatonGroup>(b =>
        {
            b.HasKey(g => g.Id);
            b.HasMany(g => g.SavedAutomatons)
             .WithOne(a => a.Group)
             .HasForeignKey(a => a.GroupId)
             .OnDelete(DeleteBehavior.Cascade);

            b.Property(g => g.UserId).IsRequired();
            b.Property(g => g.Name).IsRequired().HasMaxLength(200);
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
