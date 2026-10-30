using System.Net;
using System.Net.Http.Json;
using FlowBoard.Shared.DTOs.Common;
using FlowBoard.Shared.DTOs.Workspaces;
using FlowBoard.Web.Services.UI;

namespace FlowBoard.Web.Services.Api;

public class WorkspaceApiService : IWorkspaceApiService
{
    private readonly FlowBoardApiClient _apiClient;
    private readonly ToastService _toastService;

    public WorkspaceApiService(
        FlowBoardApiClient apiClient,
        ToastService toastService)
    {
        _apiClient = apiClient;
        _toastService = toastService;
    }

    // ============================================================
    // CREATE WORKSPACE
    // ============================================================

    public async Task<WorkspaceResponse?> CreateAsync(
        CreateWorkspaceRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.PostAsJsonAsync(
            "api/workspace",
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            _toastService.Error(
                "Unable to create workspace.");

            return null;
        }

        var workspace =
            await response.Content.ReadFromJsonAsync<WorkspaceResponse>(
                cancellationToken);

        if (workspace is not null)
        {
            _toastService.Success(
                "Workspace created successfully.");
        }

        return workspace;
    }

    // ============================================================
    // GET WORKSPACES
    // ============================================================

    public async Task<PagedResponse<WorkspaceResponse>?> GetAsync(
        PagedSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var query =
            $"api/workspace" +
            $"?Page={request.Page}" +
            $"&PageSize={request.PageSize}";

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            query +=
                $"&Search={Uri.EscapeDataString(request.Search)}";
        }

        var response = await _apiClient.GetAsync(
            query,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            _toastService.Error(
                "Unable to load workspaces.");

            return null;
        }

        return await response.Content
            .ReadFromJsonAsync<PagedResponse<WorkspaceResponse>>(
                cancellationToken);
    }

    // ============================================================
    // GET WORKSPACE BY ID
    // ============================================================

    public async Task<WorkspaceResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.GetAsync(
            $"api/workspace/{id}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _toastService.Error(
                "Workspace was not found.");

            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            _toastService.Error(
                "Unable to load workspace.");

            return null;
        }

        return await response.Content
            .ReadFromJsonAsync<WorkspaceResponse>(
                cancellationToken);
    }

    // ============================================================
    // UPDATE WORKSPACE
    // ============================================================

    public async Task<WorkspaceResponse?> UpdateAsync(
        Guid id,
        UpdateWorkspaceRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.PutAsJsonAsync(
            $"api/workspace/{id}",
            request,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _toastService.Error(
                "Workspace was not found.");

            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            _toastService.Error(
                "Unable to update workspace.");

            return null;
        }

        var workspace =
            await response.Content.ReadFromJsonAsync<WorkspaceResponse>(
                cancellationToken);

        if (workspace is not null)
        {
            _toastService.Success(
                "Workspace updated successfully.");
        }

        return workspace;
    }

    // ============================================================
    // DELETE WORKSPACE
    // ============================================================

    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.DeleteAsync(
            $"api/workspace/{id}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return false;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _toastService.Error(
                "Workspace was not found.");

            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            _toastService.Error(
                "Unable to delete workspace.");

            return false;
        }

        _toastService.Success(
            "Workspace deleted successfully.");

        return true;
    }
}