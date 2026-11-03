using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlowBoard.Shared.DTOs.Common;
using FlowBoard.Shared.DTOs.Projects;
using FlowBoard.Web.Services.UI;

namespace FlowBoard.Web.Services.Api;

public sealed class ProjectApiService : IProjectApiService
{
    private const string Root = "api/projects";
    private readonly FlowBoardApiClient _apiClient;
    private readonly ToastService _toastService;

    public ProjectApiService(
        FlowBoardApiClient apiClient,
        ToastService toastService)
    {
        _apiClient = apiClient;
        _toastService = toastService;
    }

    public async Task<ProjectResponse?> CreateAsync(
        CreateProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.PostAsJsonAsync(
            Root,
            request,
            cancellationToken);
        return await ReadAsync<ProjectResponse>(
            response,
            "Unable to create the project.",
            cancellationToken);
    }

    public Task<PagedResponse<ProjectResponse>?> GetAsync(
        ProjectQueryRequest request,
        CancellationToken cancellationToken = default) =>
        GetPageAsync<ProjectResponse>(
            $"{Root}{ProjectQuery(request)}",
            "Unable to load projects.",
            cancellationToken);

    public Task<PagedResponse<ProjectResponse>?> GetMineAsync(
        ProjectQueryRequest request,
        CancellationToken cancellationToken = default) =>
        GetPageAsync<ProjectResponse>(
            $"{Root}/mine{ProjectQuery(request)}",
            "Unable to load your projects.",
            cancellationToken);

    public async Task<ProjectResponse?> GetByIdAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.GetAsync(
            $"{Root}/{projectId}",
            cancellationToken);
        return await ReadAsync<ProjectResponse>(
            response,
            "Unable to load the project.",
            cancellationToken);
    }

    public async Task<ProjectResponse?> UpdateAsync(
        Guid projectId,
        UpdateProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.PutAsJsonAsync(
            $"{Root}/{projectId}",
            request,
            cancellationToken);
        return await ReadAsync<ProjectResponse>(
            response,
            "Unable to update the project.",
            cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.DeleteAsync(
            $"{Root}/{projectId}",
            cancellationToken);
        return await ReportSuccessAsync(
            response,
            "Unable to delete the project.",
            cancellationToken,
            "The project cannot be deleted while boards or members are associated with it.");
    }

    public Task<PagedResponse<ProjectMemberResponse>?> GetMembersAsync(
        Guid projectId,
        PagedSearchRequest request,
        CancellationToken cancellationToken = default) =>
        GetPageAsync<ProjectMemberResponse>(
            $"{Root}/{projectId}/members{PageQuery(request)}",
            "Unable to load project members.",
            cancellationToken);

    public async Task<ProjectMemberResponse?> AddMemberAsync(
        Guid projectId,
        AddProjectMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.PostAsJsonAsync(
            $"{Root}/{projectId}/members",
            request,
            cancellationToken);
        return await ReadAsync<ProjectMemberResponse>(
            response,
            "Unable to add the project member.",
            cancellationToken,
            "The user is already a member of this project.",
            "Only Employee accounts can be added as project members.");
    }

    public async Task<bool> RemoveMemberAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.DeleteAsync(
            $"{Root}/{projectId}/members/{userId}",
            cancellationToken);
        return await ReportSuccessAsync(
            response,
            "Unable to remove the project member.",
            cancellationToken,
            "This project membership cannot be removed while related board memberships or task dependencies remain.");
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
        string? conflictMessage = null,
        string? validationMessage = null)
    {
        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                await ReportFailureAsync(
                    response,
                    errorMessage,
                    cancellationToken,
                    conflictMessage,
                    validationMessage);
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
        string? conflictMessage,
        string? validationMessage = null)
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
                "You are not authorized to manage this project.",
            HttpStatusCode.NotFound =>
                "The project, workspace, or account could not be found.",
            HttpStatusCode.Conflict =>
                conflictMessage ?? "This operation conflicts with the current project relationships.",
            HttpStatusCode.BadRequest =>
                validationMessage ?? "The project request is invalid. Check the entered information.",
            _ => errorMessage
        };

        _toastService.Error(
            string.IsNullOrWhiteSpace(apiMessage) ? fallbackMessage : apiMessage);
    }

    private static string ProjectQuery(ProjectQueryRequest request)
    {
        var query = PageQuery(request);
        if (request.WorkspaceId.HasValue)
        {
            query += $"&WorkspaceId={request.WorkspaceId.Value}";
        }

        if (request.Status.HasValue)
        {
            query += $"&Status={request.Status.Value}";
        }

        return query;
    }

    private static string PageQuery(PagedRequest request)
    {
        var query = $"?Page={request.Page}&PageSize={request.PageSize}";
        if (request is PagedSearchRequest searchable &&
            !string.IsNullOrWhiteSpace(searchable.Search))
        {
            query += $"&Search={Uri.EscapeDataString(searchable.Search)}";
        }

        return query;
    }
}
