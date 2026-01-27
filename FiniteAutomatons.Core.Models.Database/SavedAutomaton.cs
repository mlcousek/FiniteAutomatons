namespace FiniteAutomatons.Core.Models.Database;

public class SavedAutomaton
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty; // Identity user id (creator)
    // switched to many-to-many assignments instead of single GroupId
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ContentJson { get; set; } = string.Empty; // serialized automaton (domain-focused JSON)
    public bool HasExecutionState { get; set; } = false;
    public string? ExecutionStateJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // assignments to groups (many-to-many)
    public List<SavedAutomatonGroupAssignment> Assignments { get; set; } = new();
}
