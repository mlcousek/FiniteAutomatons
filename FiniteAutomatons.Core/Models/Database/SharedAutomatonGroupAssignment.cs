namespace FiniteAutomatons.Core.Models.Database;

public class SharedAutomatonGroupAssignment
{
    public int Id { get; set; }

    public int AutomatonId { get; set; }

    public int GroupId { get; set; }

    public DateTime AssignedAt { get; set; }

    public SharedAutomaton? Automaton { get; set; }
    public SharedAutomatonGroup? Group { get; set; }
}
