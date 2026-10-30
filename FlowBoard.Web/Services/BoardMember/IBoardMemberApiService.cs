using FlowBoard.Shared.DTOs.BoardMembers;
using FlowBoard.Shared.DTOs.Common;

namespace FlowBoard.Web.Services.Api;

public interface IBoardMemberApiService
{
    Task<PagedResponse<BoardMemberResponse>?> GetAsync(
        Guid boardId,
        PagedSearchRequest request);

    Task<BoardMemberResponse?> AddAsync(
        Guid boardId,
        AddBoardMemberRequest request);

    Task<bool> UpdateRoleAsync(
        Guid boardId,
        Guid userId,
        UpdateBoardMemberRoleRequest request);

    Task<bool> DeleteAsync(
        Guid boardId,
        Guid userId);
}