using FlowBoard.Domain.Entities;

namespace FlowBoard.Api.Services.BoardAuthorization;

public interface IBoardAuthorizationService
{
    IQueryable<Board> GetAccessibleBoardsQuery();

    Task EnsureCanManageWorkspaceBoardsAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    Task<BoardAccessContext> GetAccessAsync(
        Guid boardId,
        CancellationToken cancellationToken = default);

    Task<BoardAccessContext> EnsureCanViewAsync(
        Guid boardId,
        CancellationToken cancellationToken = default);

    Task<BoardAccessContext> EnsureCanEditContentAsync(
        Guid boardId,
        CancellationToken cancellationToken = default);

    Task<BoardAccessContext> EnsureCanManageBoardAsync(
        Guid boardId,
        CancellationToken cancellationToken = default);

    Task<BoardAccessContext> EnsureCanDeleteBoardAsync(
        Guid boardId,
        CancellationToken cancellationToken = default);

    Task<BoardAccessContext> EnsureCanCommentAsync(
        Guid boardId,
        CancellationToken cancellationToken = default);
}
