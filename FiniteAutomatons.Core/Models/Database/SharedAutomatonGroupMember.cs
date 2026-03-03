namespace FiniteAutomatons.Core.Models.Database;

public class SharedAutomatonGroupMember
{
    public int Id { get; set; }

    public int GroupId { get; set; }

    public required string UserId { get; set; }

    public SharedGroupRole Role { get; set; }

    public DateTime JoinedAt { get; set; }

    public string? InvitedByUserId { get; set; }

    public SharedAutomatonGroup? Group { get; set; }
}
