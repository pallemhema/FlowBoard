using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlowBoard.Shared.DTOs.Boards;
using FlowBoard.Shared.DTOs.Common;
using FlowBoard.Web.Services.UI;

namespace FlowBoard.Web.Services.Api;

public class BoardApiService : IBoardApiService
{
    private readonly FlowBoardApiClient _apiClient;
    private readonly ToastService _toastService;

    public BoardApiService(
        FlowBoardApiClient apiClient,
        ToastService toastService)
    {
        _apiClient = apiClient;
        _toastService = toastService;
    }

    public async Task<PagedResponse<BoardResponse>?> GetAsync(
        BoardQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query =
            $"api/boards" +
            $"?Page={request.Page}" +
            $"&PageSize={request.PageSize}";

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            query +=
                $"&Search={Uri.EscapeDataString(request.Search)}";
        }

        if (request.WorkspaceId.HasValue)
        {
            query +=
                $"&WorkspaceId={request.WorkspaceId.Value}";
        }

        var response = await _apiClient.GetAsync(query, cancellationToken);
        return await ReadAsync<PagedResponse<BoardResponse>>(
            response,
            "Unable to load boards.",
            cancellationToken);
    }

    public async Task<BoardResponse?> GetByIdAsync(
        Guid boardId,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.GetAsync(
            $"api/boards/{boardId}",
            cancellationToken);
        return await ReadAsync<BoardResponse>(
            response,
            "Unable to load the board.",
            cancellationToken);
    }

    public async Task<BoardResponse?> CreateAsync(
        CreateBoardRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.PostAsJsonAsync(
            "api/boards",
            request,
            cancellationToken);
        var board = await ReadAsync<BoardResponse>(
            response,
            "Unable to create the board.",
            cancellationToken);

        if (board is not null)
        {
            _toastService.Success(
                "Board created successfully.");
        }

        return board;
    }

    public async Task<bool> UpdateAsync(
        Guid boardId,
        UpdateBoardRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.PutAsJsonAsync(
            $"api/boards/{boardId}",
            request,
            cancellationToken);
        var succeeded = await ReportSuccessAsync(
            response,
            "Unable to update the board.",
            cancellationToken);
        if (succeeded)
        {
            _toastService.Success("Board updated successfully.");
        }

        return succeeded;
    }

    public async Task<bool> DeleteAsync(
        Guid boardId,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.DeleteAsync(
            $"api/boards/{boardId}",
            cancellationToken);
        var succeeded = await ReportSuccessAsync(
            response,
            "Unable to delete the board.",
            cancellationToken,
            "The board cannot be deleted while columns or members are associated with it. Remove those dependencies first.");
        if (succeeded)
        {
            _toastService.Success("Board deleted successfully.");
        }

        return succeeded;
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
                await ReportFailureAsync(response, errorMessage, cancellationToken);
                return default;
            }

            return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
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
        string? conflictMessage = null)
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
            HttpStatusCode.Forbidden => "You are not authorized to manage this board.",
            HttpStatusCode.NotFound => "The board, workspace, project, or account could not be found.",
            HttpStatusCode.Conflict => conflictMessage ?? "This operation conflicts with the board's current dependencies or membership rules.",
            HttpStatusCode.BadRequest => "The board request is invalid. Check the selected purpose, workspace, and project.",
            _ => errorMessage
        };

        _toastService.Error(
            string.IsNullOrWhiteSpace(apiMessage) ? fallbackMessage : apiMessage);
    }
}