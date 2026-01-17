namespace FiniteAutomatons.Core.Models.Database;

public class SavedAutomatonGroupMember
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public SavedAutomatonGroup? Group { get; set; }
    public string UserId { get; set; } = string.Empty;
    // reserved for future roles/flags
    public bool IsModerator { get; set; } = false;
}
