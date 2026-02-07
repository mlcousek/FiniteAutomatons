namespace FiniteAutomatons.Core.Models.Database;

/// <summary>
/// Represents a user's membership in a shared group with specific role/permissions
/// </summary>
public class SharedAutomatonGroupMember
{
    public int Id { get; set; }
    
    public int GroupId { get; set; }
    
    public required string UserId { get; set; }
    
    /// <summary>
    /// The member's role in this group
    /// </summary>
    public SharedGroupRole Role { get; set; }
    
    public DateTime JoinedAt { get; set; }
    
    /// <summary>
    /// Who invited this user (null if joined via link)
    /// </summary>
    public string? InvitedByUserId { get; set; }
    
    // Navigation properties
    public SharedAutomatonGroup? Group { get; set; }
}
