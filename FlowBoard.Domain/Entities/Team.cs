namespace FlowBoard.Domain.Entities;

using FlowBoard.Domain.Common;

public class Team : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public ICollection<TeamMember> Members { get; set; } = [];
}
