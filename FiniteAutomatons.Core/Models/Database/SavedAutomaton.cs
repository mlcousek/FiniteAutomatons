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

    /// <summary>
    /// Indicates what data was saved with this automaton (structure only, with input, or with full execution state).
    /// </summary>
    public AutomatonSaveMode SaveMode { get; set; } = AutomatonSaveMode.Structure;

    public string? ExecutionStateJson { get; set; }

    /// <summary>
    /// The source regular expression if this automaton was created from a regex.
    /// </summary>
    public string? SourceRegex { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<SavedAutomatonGroupAssignment> Assignments { get; set; } = [];

    /// <summary>
    /// Checks if this automaton was saved with input (either just input or with execution state).
    /// </summary>
    public bool HasInput() => SaveMode >= AutomatonSaveMode.WithInput;
}


