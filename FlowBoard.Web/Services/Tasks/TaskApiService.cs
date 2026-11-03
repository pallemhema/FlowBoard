using System.Net;
using System.Net.Http.Json;
using FlowBoard.Shared.DTOs.CardComments;
using FlowBoard.Shared.DTOs.Cards;
using FlowBoard.Shared.DTOs.Common;
using FlowBoard.Shared.DTOs.MyTasks;
using FlowBoard.Web.Services.UI;

namespace FlowBoard.Web.Services.Api;

public sealed class TaskApiService : ITaskApiService
{
    private const string Root = "api/tasks";
    private readonly FlowBoardApiClient _apiClient;
    private readonly ToastService _toastService;

    public TaskApiService(
        FlowBoardApiClient apiClient,
        ToastService toastService)
    {
        _apiClient = apiClient;
        _toastService = toastService;
    }

    public Task<PagedResponse<MyTaskResponse>?> GetMyTasksAsync(
        MyTasksQueryRequest request,
        CancellationToken cancellationToken = default) =>
        GetPageAsync<MyTaskResponse>(
            $"{Root}/my-tasks{MyTasksQuery(request)}",
            "Unable to load your tasks.",
            cancellationToken);

    public Task<PagedResponse<CardResponse>?> GetByBoardAsync(
        Guid boardId,
        CardQueryRequest request,
        CancellationToken cancellationToken = default) =>
        GetPageAsync<CardResponse>(
            $"{Root}/board/{boardId}{CardQuery(request)}",
            "Unable to load board tasks.",
            cancellationToken);

    public Task<PagedResponse<CardResponse>?> GetByProjectAsync(
        Guid projectId,
        CardQueryRequest request,
        CancellationToken cancellationToken = default) =>
        GetPageAsync<CardResponse>(
            $"{Root}/project/{projectId}{CardQuery(request)}",
            "Unable to load project tasks.",
            cancellationToken);

    public async Task<CardResponse?> GetByIdAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.GetAsync(
            $"{Root}/{taskId}",
            cancellationToken);
        return await ReadAsync<CardResponse>(
            response,
            "Unable to load the task.",
            cancellationToken);
    }

    public async Task<CardResponse?> CreateAsync(
        Guid columnId,
        CreateCardRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.PostAsJsonAsync(
            $"{Root}/columns/{columnId}/cards",
            request,
            cancellationToken);
        return await ReadAsync<CardResponse>(
            response,
            "Unable to create the task.",
            cancellationToken);
    }

    public async Task<bool> UpdateAsync(
        Guid taskId,
        UpdateCardRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.PutAsJsonAsync(
            $"{Root}/{taskId}",
            request,
            cancellationToken);
        return ReportSuccess(response, "Unable to update the task.");
    }

    public async Task<bool> AssignAsync(
        Guid taskId,
        AssignTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.PutAsJsonAsync(
            $"{Root}/{taskId}/assignee",
            request,
            cancellationToken);
        return ReportSuccess(response, "Unable to assign the task.");
    }

    public async Task<bool> UnassignAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.DeleteAsync(
            $"{Root}/{taskId}/assignee",
            cancellationToken);
        return ReportSuccess(response, "Unable to unassign the task.");
    }

    public async Task<bool> DeleteAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.DeleteAsync(
            $"{Root}/{taskId}",
            cancellationToken);
        return ReportSuccess(response, "Unable to delete the task.");
    }

    public async Task<IReadOnlyList<CardCommentResponse>?> GetCommentsAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.GetAsync(
            $"{Root}/{taskId}/comments",
            cancellationToken);
        return await ReadAsync<IReadOnlyList<CardCommentResponse>>(
            response,
            "Unable to load task comments.",
            cancellationToken);
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
        CancellationToken cancellationToken)
    {
        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode != HttpStatusCode.Unauthorized)
                {
                    _toastService.Error(errorMessage);
                }

                return default;
            }

            return await response.Content.ReadFromJsonAsync<T>(
                cancellationToken);
        }
    }

    private bool ReportSuccess(
        HttpResponseMessage response,
        string errorMessage)
    {
        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                _toastService.Error(errorMessage);
            }

            return false;
        }
    }

    private static string CardQuery(CardQueryRequest request)
    {
        var query = PageQuery(request);
        if (request.Priority.HasValue)
        {
            query += $"&Priority={request.Priority.Value}";
        }

        if (request.AssignedUserId.HasValue)
        {
            query += $"&AssignedUserId={request.AssignedUserId.Value}";
        }

        return query;
    }

    private static string MyTasksQuery(MyTasksQueryRequest request)
    {
        var query = PageQuery(request);
        query = Append(query, "Priority", request.Priority?.ToString());
        query = Append(query, "BoardColumnId", request.BoardColumnId?.ToString());
        query = Append(query, "BoardId", request.BoardId?.ToString());
        query = Append(query, "ProjectId", request.ProjectId?.ToString());
        query = Append(query, "WorkspaceId", request.WorkspaceId?.ToString());
        query = Append(query, "DueDateFrom", request.DueDateFrom?.ToString("O"));
        query = Append(query, "DueDateTo", request.DueDateTo?.ToString("O"));
        if (request.OverdueOnly)
        {
            query = Append(query, "OverdueOnly", "true");
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

    private static string Append(
        string query,
        string name,
        string? value)
    {
        return value is null
            ? query
            : $"{query}&{name}={Uri.EscapeDataString(value)}";
    }
}
