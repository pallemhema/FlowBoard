using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlowBoard.Shared.DTOs.Common;
using FlowBoard.Shared.DTOs.Workspaces;
using FlowBoard.Web.Services.UI;

namespace FlowBoard.Web.Services.Api;

public class WorkspaceApiService : IWorkspaceApiService
{
    private const string Root = "api/workspace";
    private readonly FlowBoardApiClient _apiClient;
    private readonly ToastService _toastService;

    public WorkspaceApiService(
        FlowBoardApiClient apiClient,
        ToastService toastService)
    {
        _apiClient = apiClient;
        _toastService = toastService;
    }

    public async Task<WorkspaceResponse?> CreateAsync(
        CreateWorkspaceRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.PostAsJsonAsync(
            Root,
            request,
            cancellationToken);

        var workspace = await ReadAsync<WorkspaceResponse>(
            response,
            "Unable to create the workspace.",
            cancellationToken);

        if (workspace is not null)
        {
            _toastService.Success("Workspace created successfully.");
        }

        return workspace;
    }

    public Task<PagedResponse<WorkspaceResponse>?> GetAsync(
        PagedSearchRequest request,
        CancellationToken cancellationToken = default) =>
        GetPageAsync<WorkspaceResponse>(
            $"{Root}{PageQuery(request)}",
            "Unable to load workspaces.",
            cancellationToken);

    public async Task<WorkspaceResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.GetAsync(
            $"{Root}/{id}",
            cancellationToken);

        return await ReadAsync<WorkspaceResponse>(
            response,
            "Unable to load the workspace.",
            cancellationToken);
    }

    public async Task<WorkspaceResponse?> UpdateAsync(
        Guid id,
        UpdateWorkspaceRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.PutAsJsonAsync(
            $"{Root}/{id}",
            request,
            cancellationToken);

        var workspace = await ReadAsync<WorkspaceResponse>(
            response,
            "Unable to update the workspace.",
            cancellationToken);

        if (workspace is not null)
        {
            _toastService.Success("Workspace updated successfully.");
        }

        return workspace;
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.DeleteAsync(
            $"{Root}/{id}",
            cancellationToken);

        var deleted = await ReportSuccessAsync(
            response,
            "Unable to delete the workspace.",
            cancellationToken,
            "The workspace cannot be deleted while projects, boards, or members are associated with it. Remove or relocate those relationships first.");

        if (deleted)
        {
            _toastService.Success("Workspace deleted successfully.");
        }

        return deleted;
    }

    public Task<PagedResponse<WorkspaceMemberResponse>?> GetMembersAsync(
        Guid workspaceId,
        PagedSearchRequest request,
        CancellationToken cancellationToken = default) =>
        GetPageAsync<WorkspaceMemberResponse>(
            $"{Root}/{workspaceId}/members{PageQuery(request)}",
            "Unable to load workspace members.",
            cancellationToken);

    public async Task<WorkspaceMemberResponse?> AddMemberAsync(
        Guid workspaceId,
        AddWorkspaceMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.PostAsJsonAsync(
            $"{Root}/{workspaceId}/members",
            request,
            cancellationToken);

        var member = await ReadAsync<WorkspaceMemberResponse>(
            response,
            "Unable to add the workspace member.",
            cancellationToken,
            "This account is already a member of the workspace.");

        if (member is not null)
        {
            _toastService.Success("Workspace member added successfully.");
        }

        return member;
    }

    public async Task<bool> RemoveMemberAsync(
        Guid workspaceId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.DeleteAsync(
            $"{Root}/{workspaceId}/members/{userId}",
            cancellationToken);

        var removed = await ReportSuccessAsync(
            response,
            "Unable to remove the workspace member.",
            cancellationToken);

        if (removed)
        {
            _toastService.Success("Workspace member removed successfully.");
        }

        return removed;
    }

    private async Task<PagedResponse<T>?> GetPageAsync<T>(
        string url,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        var response = await _apiClient.GetAsync(url, cancellationToken);
        return await ReadAsync<PagedResponse<T>>(
            response,
            errorMessage,
            cancellationToken);
    }

    private async Task<T?> ReadAsync<T>(
        HttpResponseMessage response,
        string errorMessage,
        CancellationToken cancellationToken,
        string? conflictMessage = null)
    {
        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                await ReportFailureAsync(
                    response,
                    errorMessage,
                    cancellationToken,
                    conflictMessage);
                return default;
            }

            return await response.Content.ReadFromJsonAsync<T>(
                cancellationToken);
        }
    }

    private async Task<bool> ReportSuccessAsync(
        HttpResponseMessage response,
        string errorMessage,
        CancellationToken cancellationToken,
        string? conflictMessage = null)
    {
        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            await ReportFailureAsync(
                response,
                errorMessage,
                cancellationToken,
                conflictMessage);
            return false;
        }
    }

    private async Task ReportFailureAsync(
        HttpResponseMessage response,
        string errorMessage,
        CancellationToken cancellationToken,
        string? conflictMessage)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return;
        }

        string? apiMessage = null;
        try
        {
            apiMessage = (await response.Content
                .ReadFromJsonAsync<ErrorResponse>(cancellationToken))?.Message;
        }
        catch (JsonException)
        {
        }

        var fallbackMessage = response.StatusCode switch
        {
            HttpStatusCode.Forbidden =>
                "You are not authorized to manage this workspace.",
            HttpStatusCode.NotFound =>
                "The workspace or account could not be found. Refresh and try again.",
            HttpStatusCode.Conflict =>
                conflictMessage ?? "This operation conflicts with the current workspace relationships.",
            HttpStatusCode.BadRequest =>
                "The workspace request is invalid. Check the entered information.",
            _ => errorMessage
        };

        _toastService.Error(
            string.IsNullOrWhiteSpace(apiMessage) ? fallbackMessage : apiMessage);
    }

    private static string PageQuery(PagedRequest request)
    {
        var query = $"?Page={request.Page}&PageSize={request.PageSize}";
        return request is PagedSearchRequest searchable &&
               !string.IsNullOrWhiteSpace(searchable.Search)
            ? $"{query}&Search={Uri.EscapeDataString(searchable.Search)}"
            : query;
    }
}
