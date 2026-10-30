using FlowBoard.Shared.DTOs.Common;
using FlowBoard.Shared.DTOs.Teams;

namespace FlowBoard.Api.Services.Teams;

public interface ITeamService
{
    Task<TeamResponse> CreateAsync(
        CreateTeamRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<TeamResponse>> GetAllAsync(
        PagedSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<TeamResponse>> GetMineAsync(
        PagedSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<TeamResponse> GetByIdAsync(
        Guid teamId,
        CancellationToken cancellationToken = default);

    Task<TeamResponse> UpdateAsync(
        Guid teamId,
        UpdateTeamRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid teamId,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<TeamMemberResponse>> GetMembersAsync(
        Guid teamId,
        PagedSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<TeamMemberResponse> AddMemberAsync(
        Guid teamId,
        AddTeamMemberRequest request,
        CancellationToken cancellationToken = default);

    Task RemoveMemberAsync(
        Guid teamId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
