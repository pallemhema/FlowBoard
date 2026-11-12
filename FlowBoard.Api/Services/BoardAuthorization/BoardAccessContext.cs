using FlowBoard.Domain.Entities;
using FlowBoard.Domain.Enums;

namespace FlowBoard.Api.Services.BoardAuthorization;

public sealed class BoardAccessContext
{
    public required Board Board { get; init; }

    public required bool IsAdmin { get; init; }

    public required bool IsBoardOwner { get; init; }

    public required bool IsWorkspaceOwner { get; init; }

    public required bool IsProjectManager { get; init; }

    public required bool IsWorkspaceManager { get; init; }

    public BoardMemberRole? MemberRole { get; init; }





    public bool CanView =>
        IsAdmin ||
        IsBoardOwner ||
        IsWorkspaceOwner ||
        IsWorkspaceManager ||
        MemberRole.HasValue;
    public bool CanManageBoard =>
        IsAdmin ||
        IsBoardOwner ||
        IsWorkspaceOwner ||
        IsWorkspaceManager;

    public bool CanEditContent => CanManageBoard;

    public bool CanDeleteBoard =>
        IsAdmin ||
        IsWorkspaceOwner;

    public bool CanComment => CanView;
}
