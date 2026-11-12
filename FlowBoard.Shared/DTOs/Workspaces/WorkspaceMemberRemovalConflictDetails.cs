namespace FlowBoard.Shared.DTOs.Workspaces;

public class WorkspaceMemberRemovalConflictDetails
{
    public IReadOnlyList<WorkspaceBoardMembershipDetails> BlockingBoardMemberships { get; set; } = [];
}

public class WorkspaceBoardMembershipDetails
{
    public Guid BoardId { get; set; }

    public string BoardName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;
}
