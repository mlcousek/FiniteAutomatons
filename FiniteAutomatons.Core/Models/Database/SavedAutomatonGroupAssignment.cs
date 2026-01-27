namespace FiniteAutomatons.Core.Models.Database;

public class SavedAutomatonGroupAssignment
{
    public int Id { get; set; }
    public int AutomatonId { get; set; }
    public SavedAutomaton? Automaton { get; set; }
    public int GroupId { get; set; }
    public SavedAutomatonGroup? Group { get; set; }
}
