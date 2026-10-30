using FlowBoard.Domain.Enums;

namespace FlowBoard.Domain.Entities;

public class BoardMember
{
    public Guid BoardId { get; set; }

    public Guid UserId { get; set; }

    public BoardMemberRole Role { get; set; }

    public DateTime JoinedAt { get; set; }

    public Board Board { get; set; } = null!;

    public User User { get; set; } = null!;
}