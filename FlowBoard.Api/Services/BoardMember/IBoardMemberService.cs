using FlowBoard.Shared.DTOs.BoardMembers;
using FlowBoard.Shared.DTOs.Common;

namespace FlowBoard.Api.Services.BoardMembers;

public interface IBoardMemberService
{
    Task<BoardMemberResponse> AddAsync(
        Guid boardId,
        AddBoardMemberRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<BoardMemberResponse>> GetMembersAsync(
        Guid boardId,
        PagedSearchRequest request,
        CancellationToken cancellationToken = default);

    Task UpdateRoleAsync(
        Guid boardId,
        Guid userId,
        UpdateBoardMemberRoleRequest request,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(
        Guid boardId,
        Guid userId,
        CancellationToken cancellationToken = default);
}