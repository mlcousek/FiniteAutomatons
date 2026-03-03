namespace FiniteAutomatons.Core.Models.Database;

public enum InvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2,
    Expired = 3,
    Cancelled = 4
}

public class SharedAutomatonGroupInvitation
{
    public int Id { get; set; }

    public int GroupId { get; set; }

    public required string Email { get; set; }

    public SharedGroupRole Role { get; set; }

    public required string InvitedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public required string Token { get; set; }

    public InvitationStatus Status { get; set; }

    public DateTime? ResponsedAt { get; set; }

    public SharedAutomatonGroup? Group { get; set; }
}
