using FlowBoard.Shared.DTOs.BoardColumns;

namespace FlowBoard.Web.Services.Api;

public interface IBoardColumnApiService
{
    Task<BoardColumnResponse?> CreateAsync(
        Guid boardId,
        CreateBoardColumnRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BoardColumnResponse>?> GetAllAsync(
        Guid boardId,
        CancellationToken cancellationToken = default);

    Task<BoardColumnResponse?> GetAsync(
        Guid boardId,
        Guid columnId,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(
        Guid boardId,
        Guid columnId,
        UpdateBoardColumnRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid boardId,
        Guid columnId,
        CancellationToken cancellationToken = default);
}