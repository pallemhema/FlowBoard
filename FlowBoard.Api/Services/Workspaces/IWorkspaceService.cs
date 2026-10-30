using FlowBoard.Shared.DTOs.Workspaces;
using FlowBoard.Shared.DTOs.Common;

namespace FlowBoard.Api.Services.Workspaces;

public interface IWorkspaceService
{
    Task<WorkspaceResponse> CreateAsync(
        CreateWorkspaceRequest request,
        CancellationToken cancellationToken = default);


    Task<PagedResponse<WorkspaceResponse>> GetAsync(
        PagedSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkspaceResponse> GetByIdAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    Task<WorkspaceResponse> UpdateAsync(
        Guid workspaceId,
        UpdateWorkspaceRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<WorkspaceMemberResponse>> GetMembersAsync(
        Guid workspaceId,
        PagedSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkspaceMemberResponse> AddMemberAsync(
        Guid workspaceId,
        AddWorkspaceMemberRequest request,
        CancellationToken cancellationToken = default);

    Task RemoveMemberAsync(
        Guid workspaceId,
        Guid userId,
        CancellationToken cancellationToken = default);
}