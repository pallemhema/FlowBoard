namespace FlowBoard.Domain.Entities;

using FlowBoard.Domain.Common;
using FlowBoard.Domain.Enums;

public class Project : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Guid WorkspaceId { get; set; }

    public Guid ManagerId { get; set; }

    public ProjectStatus Status { get; set; } = ProjectStatus.Active;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public Workspace Workspace { get; set; } = null!;

    public User Manager { get; set; } = null!;

    public ICollection<ProjectMember> Members { get; set; } = [];

    public ICollection<Board> Boards { get; set; } = [];
}
