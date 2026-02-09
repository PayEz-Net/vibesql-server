namespace VibeSQL.Core.Entities;

/// <summary>
/// Represents a task from the kanban_tasks or agent_kanban_tasks table.
/// </summary>
public class KanbanTask
{
    public int TaskId { get; set; }
    public int? BoardId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "backlog";
    public string? Lane { get; set; }
    public string Priority { get; set; } = "normal";
    public string? AssignedTo { get; set; }
    public int? AssignedAgentId { get; set; }
    public string? CreatedBy { get; set; }
    public int? CreatedByAgentId { get; set; }
    public string? SpecPath { get; set; }
    public List<string>? FilesChanged { get; set; }
    public string? Blockers { get; set; }
    public List<string>? Labels { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
