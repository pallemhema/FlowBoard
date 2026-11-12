using FlowBoard.Shared.DTOs.Auth;
using FlowBoard.Shared.Enums;

namespace FlowBoard.Shared.DTOs.Projects;

public class ProjectResponse
{
    public Guid ProjectId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Guid WorkspaceId { get; set; }

    public string WorkspaceName { get; set; } = string.Empty;

    public Guid ManagerId { get; set; }

    public UserDirectoryResponse Manager { get; set; } = new();

    public ProjectStatus Status { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ModifiedAt { get; set; }
}
