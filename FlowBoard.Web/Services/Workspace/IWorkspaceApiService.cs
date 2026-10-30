
using FlowBoard.Shared.DTOs.Common;
using FlowBoard.Shared.DTOs.Workspaces;

public interface IWorkspaceApiService
{
    Task<WorkspaceResponse?> CreateAsync(
            CreateWorkspaceRequest request,
            CancellationToken cancellationToken = default);
    

    Task<PagedResponse<WorkspaceResponse>?> GetAsync(
        PagedSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkspaceResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<WorkspaceResponse?> UpdateAsync(
        Guid id,
        UpdateWorkspaceRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}