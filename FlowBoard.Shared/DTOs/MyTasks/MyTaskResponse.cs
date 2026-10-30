using FlowBoard.Shared.Enums;

namespace FlowBoard.Shared.DTOs.MyTasks;

public class MyTaskResponse
{
    public Guid CardId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public CardPriority Priority { get; set; }

    public DateTime? DueDate { get; set; }

    public Guid BoardColumnId { get; set; }

    public string ColumnName { get; set; } = string.Empty;

    public Guid BoardId { get; set; }

    public string BoardName { get; set; } = string.Empty;

    public Guid WorkspaceId { get; set; }

    public string WorkspaceName { get; set; } = string.Empty;

    public Guid? ProjectId { get; set; }

    public string? ProjectName { get; set; }

    public Guid? AssignedUserId { get; set; }

    public string? AssignedUserName { get; set; }
}
