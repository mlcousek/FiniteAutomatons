namespace FiniteAutomatons.Core.Models.Database;

/// <summary>
/// Status of a group invitation
/// </summary>
public enum InvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2,
    Expired = 3,
    Cancelled = 4
}

/// <summary>
/// Represents a pending invitation to join a shared group via email
/// </summary>
public class SharedAutomatonGroupInvitation
{
    public int Id { get; set; }
    
    public int GroupId { get; set; }
    
    /// <summary>
    /// Email address of the invited user
    /// </summary>
    public required string Email { get; set; }
    
    /// <summary>
    /// Role the user will receive upon accepting
    /// </summary>
    public SharedGroupRole Role { get; set; }
    
    /// <summary>
    /// Who sent the invitation
    /// </summary>
    public required string InvitedByUserId { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime? ExpiresAt { get; set; }
    
    /// <summary>
    /// Unique token for accepting the invitation
    /// </summary>
    public required string Token { get; set; }
    
    public InvitationStatus Status { get; set; }
    
    public DateTime? ResponsedAt { get; set; }
    
    // Navigation properties
    public SharedAutomatonGroup? Group { get; set; }
}
