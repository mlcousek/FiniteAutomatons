namespace FiniteAutomatons.Core.Models.Database;

public class SavedAutomatonGroup
{
    public int Id { get; set; }

    // Owner / admin of the group
    public string UserId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool MembersCanShare { get; set; } = true;
    public List<SavedAutomaton> SavedAutomatons { get; set; } = new();
    // assignments for many-to-many relation (automaton can be in multiple groups)
    public List<SavedAutomatonGroupAssignment> Assignments { get; set; } = new();
    public List<SavedAutomatonGroupMember> Members { get; set; } = new();
}
