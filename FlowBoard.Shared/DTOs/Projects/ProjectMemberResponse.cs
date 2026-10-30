using FlowBoard.Shared.DTOs.Auth;

namespace FlowBoard.Shared.DTOs.Projects;

public class ProjectMemberResponse
{
    public Guid ProjectId { get; set; }

    public Guid UserId { get; set; }

    public UserDirectoryResponse User { get; set; } = new();

    public DateTime JoinedAt { get; set; }
}
