using FlowBoard.Shared.DTOs.Common;
using FlowBoard.Shared.DTOs.Projects;

namespace FlowBoard.Web.Services.Api;

public interface IProjectApiService
{
    Task<ProjectResponse?> CreateAsync(
        CreateProjectRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<ProjectResponse>?> GetAsync(
        ProjectQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<ProjectResponse>?> GetMineAsync(
        ProjectQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<ProjectResponse?> GetByIdAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<ProjectResponse?> UpdateAsync(
        Guid projectId,
        UpdateProjectRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<ProjectMemberResponse>?> GetMembersAsync(
        Guid projectId,
        PagedSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<ProjectMemberResponse?> AddMemberAsync(
        Guid projectId,
        AddProjectMemberRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveMemberAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
