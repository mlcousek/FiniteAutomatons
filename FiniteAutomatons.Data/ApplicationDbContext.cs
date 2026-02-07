using FiniteAutomatons.Core.Models.Database;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace FiniteAutomatons.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
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
    
    // Shared Automatons (Collaborative)
    public DbSet<SharedAutomaton> SharedAutomatons { get; set; } = null!;
    public DbSet<SharedAutomatonGroup> SharedAutomatonGroups { get; set; } = null!;
    public DbSet<SharedAutomatonGroupMember> SharedAutomatonGroupMembers { get; set; } = null!;
    public DbSet<SharedAutomatonGroupAssignment> SharedAutomatonGroupAssignments { get; set; } = null!;
    public DbSet<SharedAutomatonGroupInvitation> SharedAutomatonGroupInvitations { get; set; } = null!;

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
        
        // Shared Automatons Configuration
        modelBuilder.Entity<SharedAutomatonGroup>(b =>
        {
            b.HasKey(g => g.Id);
            b.Property(g => g.UserId).IsRequired();
            b.Property(g => g.Name).IsRequired().HasMaxLength(200);
            b.Property(g => g.InviteCode).HasMaxLength(50);
            b.Property(g => g.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            
            b.HasIndex(g => g.InviteCode).IsUnique().HasFilter("[InviteCode] IS NOT NULL");
            
            b.HasMany(g => g.Members)
             .WithOne(m => m.Group)
             .HasForeignKey(m => m.GroupId)
             .OnDelete(DeleteBehavior.Cascade);
             
            b.HasMany(g => g.PendingInvitations)
             .WithOne(i => i.Group)
             .HasForeignKey(i => i.GroupId)
             .OnDelete(DeleteBehavior.Cascade);
        });
        
        modelBuilder.Entity<SharedAutomaton>(b =>
        {
            b.HasKey(a => a.Id);
            b.Property(a => a.CreatedByUserId).IsRequired();
            b.Property(a => a.Name).IsRequired().HasMaxLength(200);
            b.Property(a => a.ContentJson).IsRequired();
            b.Property(a => a.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            b.Property(a => a.SaveMode).HasDefaultValue(AutomatonSaveMode.Structure);
        });
        
        modelBuilder.Entity<SharedAutomatonGroupMember>(b =>
        {
            b.HasKey(m => m.Id);
            b.Property(m => m.UserId).IsRequired();
            b.Property(m => m.JoinedAt).HasDefaultValueSql("GETUTCDATE()");
            
            // Unique constraint: one user can only be a member once per group
            b.HasIndex(m => new { m.GroupId, m.UserId }).IsUnique();
        });
        
        modelBuilder.Entity<SharedAutomatonGroupAssignment>(b =>
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
             
            b.Property(a => a.AssignedAt).HasDefaultValueSql("GETUTCDATE()");
            
            // Unique constraint: one automaton can only be assigned once to a group
            b.HasIndex(a => new { a.AutomatonId, a.GroupId }).IsUnique();
        });
        
        modelBuilder.Entity<SharedAutomatonGroupInvitation>(b =>
        {
            b.HasKey(i => i.Id);
            b.Property(i => i.Email).IsRequired().HasMaxLength(256);
            b.Property(i => i.Token).IsRequired().HasMaxLength(100);
            b.Property(i => i.InvitedByUserId).IsRequired();
            b.Property(i => i.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            
            b.HasIndex(i => i.Token).IsUnique();
            b.HasIndex(i => new { i.GroupId, i.Email, i.Status });
        });
    }
}
