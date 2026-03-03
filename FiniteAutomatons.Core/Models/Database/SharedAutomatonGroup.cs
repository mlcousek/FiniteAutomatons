namespace FiniteAutomatons.Core.Models.Database;

public enum SharedGroupRole
{
    Viewer = 0,

    Contributor = 1,

    Editor = 2,

    Admin = 3,

    Owner = 4
}

public class SharedAutomatonGroup
{
    public int Id { get; set; }

    public required string UserId { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? InviteCode { get; set; }

    public SharedGroupRole DefaultRoleForInvite { get; set; } = SharedGroupRole.Viewer;

    public bool IsInviteLinkActive { get; set; }

    public DateTime? InviteLinkExpiresAt { get; set; }

    public ICollection<SharedAutomatonGroupMember> Members { get; set; } = [];
    public ICollection<SharedAutomatonGroupAssignment> Assignments { get; set; } = [];
    public ICollection<SharedAutomatonGroupInvitation> PendingInvitations { get; set; } = [];
}
