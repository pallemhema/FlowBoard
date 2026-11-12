using FlowBoard.Shared.DTOs.Boards;
using FlowBoard.Shared.DTOs.Common;

namespace FlowBoard.Api.Services.Boards;

public interface IBoardService
{
    Task<BoardResponse> CreateAsync(
        CreateBoardRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<BoardResponse>> GetAsync(
        BoardQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BoardProjectOptionResponse>> GetWorkspaceProjectsAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    Task<BoardResponse> GetByIdAsync(
        Guid boardId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Guid boardId,
        UpdateBoardRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid boardId,
        CancellationToken cancellationToken = default);
}