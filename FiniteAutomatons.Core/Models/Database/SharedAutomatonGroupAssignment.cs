namespace FiniteAutomatons.Core.Models.Database;

/// <summary>
/// Many-to-many relationship between shared automatons and groups
/// </summary>
public class SharedAutomatonGroupAssignment
{
    public int Id { get; set; }
    
    public int AutomatonId { get; set; }
    
    public int GroupId { get; set; }
    
    public DateTime AssignedAt { get; set; }
    
    // Navigation properties
    public SharedAutomaton? Automaton { get; set; }
    public SharedAutomatonGroup? Group { get; set; }
}
