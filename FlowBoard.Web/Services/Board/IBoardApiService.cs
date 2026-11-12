using FlowBoard.Shared.DTOs.Boards;
using FlowBoard.Shared.DTOs.Common;

namespace FlowBoard.Web.Services.Api;

public interface IBoardApiService
{
    Task<PagedResponse<BoardResponse>?> GetAsync(
        BoardQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BoardProjectOptionResponse>?> GetWorkspaceProjectsAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    Task<BoardResponse?> GetByIdAsync(
        Guid boardId,
        CancellationToken cancellationToken = default);

    Task<BoardResponse?> CreateAsync(
        CreateBoardRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(
        Guid boardId,
        UpdateBoardRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid boardId,
        CancellationToken cancellationToken = default);
}