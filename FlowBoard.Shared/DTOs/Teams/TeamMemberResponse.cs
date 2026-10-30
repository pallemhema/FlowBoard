using FlowBoard.Shared.DTOs.Auth;

namespace FlowBoard.Shared.DTOs.Teams;

public class TeamMemberResponse
{
    public Guid TeamId { get; set; }

    public Guid UserId { get; set; }

    public UserDirectoryResponse User { get; set; } = new();

    public DateTime JoinedAt { get; set; }
}
