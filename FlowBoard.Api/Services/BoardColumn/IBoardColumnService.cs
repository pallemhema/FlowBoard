using FlowBoard.Shared.DTOs.BoardColumns;

namespace FlowBoard.Api.Services.BoardColumns;

public interface IBoardColumnService
{
    Task<BoardColumnResponse> CreateAsync(
        Guid boardId,
        CreateBoardColumnRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BoardColumnResponse>> GetAllAsync(
        Guid boardId,
        CancellationToken cancellationToken = default);

    Task<BoardColumnResponse> UpdateAsync(
        Guid boardId,
        Guid columnId,
        UpdateBoardColumnRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid boardId,
        Guid columnId,
        CancellationToken cancellationToken = default);
}