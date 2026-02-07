namespace FiniteAutomatons.Core.Models.Database;

/// <summary>
/// Role assigned to a group member
/// </summary>
public enum SharedGroupRole
{
    /// <summary>
    /// Can view automatons only
    /// </summary>
    Viewer = 0,
    
    /// <summary>
    /// Can view and add automatons
    /// </summary>
    Contributor = 1,
    
    /// <summary>
    /// Can view, add, edit, and delete automatons
    /// </summary>
    Editor = 2,
    
    /// <summary>
    /// Full control including member management
    /// </summary>
    Admin = 3,
    
    /// <summary>
    /// Creator of the group, cannot be removed
    /// </summary>
    Owner = 4
}

/// <summary>
/// Represents a collaborative group where multiple users can share automatons.
/// </summary>
public class SharedAutomatonGroup
{
    public int Id { get; set; }
    
    /// <summary>
    /// The user who created this group (always has Owner role)
    /// </summary>
    public required string UserId { get; set; }
    
    public required string Name { get; set; }
    
    public string? Description { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Unique code for sharing the group via link
    /// </summary>
    public string? InviteCode { get; set; }
    
    /// <summary>
    /// Default role for users joining via invite link
    /// </summary>
    public SharedGroupRole DefaultRoleForInvite { get; set; } = SharedGroupRole.Viewer;
    
    /// <summary>
    /// Is the invite link currently active
    /// </summary>
    public bool IsInviteLinkActive { get; set; }
    
    /// <summary>
    /// When the invite link expires (null = no expiration)
    /// </summary>
    public DateTime? InviteLinkExpiresAt { get; set; }
    
    // Navigation properties
    public ICollection<SharedAutomatonGroupMember> Members { get; set; } = [];
    public ICollection<SharedAutomatonGroupAssignment> Assignments { get; set; } = [];
    public ICollection<SharedAutomatonGroupInvitation> PendingInvitations { get; set; } = [];
}
