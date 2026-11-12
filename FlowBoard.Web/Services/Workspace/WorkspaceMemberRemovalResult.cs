using FlowBoard.Shared.DTOs.Workspaces;

namespace FlowBoard.Web.Services.Api;

public sealed record WorkspaceMemberRemovalResult(
    bool Removed,
    string? Message = null,
    WorkspaceMemberRemovalConflictDetails? ConflictDetails = null);
