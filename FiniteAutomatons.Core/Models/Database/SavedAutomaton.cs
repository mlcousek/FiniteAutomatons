namespace FiniteAutomatons.Core.Models.Database;

public class SavedAutomaton
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty; // Identity user id (creator)
    public int? GroupId { get; set; }
    public SavedAutomatonGroup? Group { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ContentJson { get; set; } = string.Empty; // serialized automaton (domain-focused JSON)
    public bool HasExecutionState { get; set; } = false;
    public string? ExecutionStateJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class SavedAutomatonGroup
{
    public int Id { get; set; }

    // Owner / admin of the group
    public string UserId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // If true, members (non-admins) are allowed to save automatons into this group.
    // If false, only the group owner (UserId) may save automatons into the group.
    public bool MembersCanShare { get; set; } = true;

    // navigation: saved automatons in this group
    public List<SavedAutomaton> SavedAutomatons { get; set; } = new();

    // navigation: members of this group (excluding owner)
    public List<SavedAutomatonGroupMember> Members { get; set; } = new();
}

public class SavedAutomatonGroupMember
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public SavedAutomatonGroup? Group { get; set; }
    public string UserId { get; set; } = string.Empty;
    // reserved for future roles/flags
    public bool IsModerator { get; set; } = false;
}
