using FlowBoard.Domain.Entities;

namespace FlowBoard.Api.Services.BoardAuthorization;

public interface IBoardAuthorizationService
{
    IQueryable<Board> GetAccessibleBoardsQuery();

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

    Task<BoardAccessContext> EnsureCanCommentAsync(
        Guid boardId,
        CancellationToken cancellationToken = default);
}
