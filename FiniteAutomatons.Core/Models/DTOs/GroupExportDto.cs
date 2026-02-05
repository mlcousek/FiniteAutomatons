namespace FiniteAutomatons.Core.Models.DTOs;

public class GroupExportDto
{
    public string GroupName { get; set; } = string.Empty;
    public string? GroupDescription { get; set; }
    public DateTime ExportedAt { get; set; }
    public List<AutomatonExportItemDto> Automatons { get; set; } = [];
}

public class AutomatonExportItemDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool HasExecutionState { get; set; }
    public AutomatonPayloadDto Content { get; set; } = new();
    public SavedExecutionStateDto? ExecutionState { get; set; }
}
