namespace FlowBoard.Domain.Entities;

using FlowBoard.Domain.Common;
using FlowBoard.Domain.Enums;

public class Board : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Guid OwnerId { get; set; }

    public User Owner { get; set; } = null!;

    public Guid WorkspaceId { get; set; }

    public Workspace Workspace { get; set; } = null!;

    public Guid? ProjectId { get; set; }

    public Project? Project { get; set; }

    public BoardPurpose Purpose { get; set; } = BoardPurpose.Project;

    public ICollection<BoardMember> Members { get; set; } = [];

    public ICollection<BoardColumn> Columns { get; set; } = [];
}