using System.Net;
using System.Net.Http.Json;
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

        var response =
            await _apiClient.GetAsync(
                query,
                cancellationToken);

        if (response.StatusCode ==
            HttpStatusCode.Unauthorized)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            _toastService.Error(
                "Unable to load boards.");

            return null;
        }

        return await response.Content
            .ReadFromJsonAsync<PagedResponse<BoardResponse>>(
                cancellationToken);
    }

    public async Task<BoardResponse?> GetByIdAsync(
        Guid boardId,
        CancellationToken cancellationToken = default)
    {
        var response =
            await _apiClient.GetAsync(
                $"api/boards/{boardId}",
                cancellationToken);

        if (response.StatusCode ==
            HttpStatusCode.Unauthorized)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            _toastService.Error(
                "Unable to load the board.");

            return null;
        }

        return await response.Content
            .ReadFromJsonAsync<BoardResponse>(
                cancellationToken);
    }

    public async Task<BoardResponse?> CreateAsync(
        CreateBoardRequest request,
        CancellationToken cancellationToken = default)
    {
        var response =
            await _apiClient.PostAsJsonAsync(
                "api/boards",
                request,
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _toastService.Error(
                "Unable to create the board.");

            return null;
        }

        var board =
            await response.Content
                .ReadFromJsonAsync<BoardResponse>(
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
        var response =
            await _apiClient.PutAsJsonAsync(
                $"api/boards/{boardId}",
                request,
                cancellationToken);

        if (response.StatusCode ==
            HttpStatusCode.Unauthorized)
        {
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            _toastService.Error(
                "Unable to update the board.");

            return false;
        }

        _toastService.Success(
            "Board updated successfully.");

        return true;
    }

    public async Task<bool> DeleteAsync(
        Guid boardId,
        CancellationToken cancellationToken = default)
    {
        var response =
            await _apiClient.DeleteAsync(
                $"api/boards/{boardId}",
                cancellationToken);

        if (response.StatusCode ==
            HttpStatusCode.Unauthorized)
        {
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            _toastService.Error(
                "Unable to delete the board.");

            return false;
        }

        _toastService.Success(
            "Board deleted successfully.");

        return true;
    }
}