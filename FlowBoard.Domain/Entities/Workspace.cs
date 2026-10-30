namespace FlowBoard.Domain.Entities;
using FlowBoard.Domain.Common;

public class Workspace : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Guid OwnerId { get; set; }

    public User Owner { get; set; } = null!;

    public ICollection<Board> Boards { get; set; } = new List<Board>();

    public ICollection<WorkspaceMember> Members { get; set; } = [];

    public ICollection<Project> Projects { get; set; } = [];
}