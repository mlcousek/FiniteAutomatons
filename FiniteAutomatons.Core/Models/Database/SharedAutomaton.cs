namespace FiniteAutomatons.Core.Models.Database;

/// <summary>
/// Represents an automaton shared within a collaborative group.
/// Multiple users can access and modify shared automatons based on their role.
/// </summary>
public class SharedAutomaton
{
    public int Id { get; set; }
    
    /// <summary>
    /// The user who created/uploaded this automaton to the shared group
    /// </summary>
    public required string CreatedByUserId { get; set; }
    
    public required string Name { get; set; }
    
    public string? Description { get; set; }
    
    /// <summary>
    /// JSON serialized automaton structure (states, transitions)
    /// </summary>
    public required string ContentJson { get; set; }
    
    /// <summary>
    /// Determines what execution data was saved with this automaton
    /// </summary>
    public AutomatonSaveMode SaveMode { get; set; }
    
    /// <summary>
    /// JSON serialized execution state (input, position, current states, etc.)
    /// </summary>
    public string? ExecutionStateJson { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime? ModifiedAt { get; set; }
    
    /// <summary>
    /// Last user who modified this automaton
    /// </summary>
    public string? ModifiedByUserId { get; set; }
    
    // Navigation properties
    public ICollection<SharedAutomatonGroupAssignment> Assignments { get; set; } = [];
    
    /// <summary>
    /// Checks if this automaton has input data saved
    /// </summary>
    public bool HasInput() => SaveMode >= AutomatonSaveMode.WithInput;
}
