namespace FiniteAutomatons.Core.Models.Database;

public class SavedAutomatonGroup
{
    public int Id { get; set; }

    // Owner / admin of the group
    public string UserId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool MembersCanShare { get; set; } = true;
    public List<SavedAutomaton> SavedAutomatons { get; set; } = [];
    public List<SavedAutomatonGroupMember> Members { get; set; } = [];
}
