using FlowBoard.Shared.DTOs.Auth;

namespace FlowBoard.Shared.DTOs.Workspaces;

public class WorkspaceMemberResponse
{
    public Guid WorkspaceId { get; set; }

    public Guid UserId { get; set; }

    public UserDirectoryResponse User { get; set; } = new();

    public DateTime JoinedAt { get; set; }
}
