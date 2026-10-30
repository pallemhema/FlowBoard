using FlowBoard.Shared.Enums;

namespace FlowBoard.Shared.DTOs.BoardMembers;

public class BoardMemberResponse
{
    public Guid UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public BoardMemberRole Role { get; set; }

    public DateTime JoinedAt { get; set; }
}