using System.Net;
using System.Net.Http.Json;
using FlowBoard.Shared.DTOs.BoardColumns;

namespace FlowBoard.Web.Services.Api;

public class BoardColumnApiService : IBoardColumnApiService
{
    private readonly FlowBoardApiClient _apiClient;

    public BoardColumnApiService(
        FlowBoardApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<BoardColumnResponse?> CreateAsync(
        Guid boardId,
        CreateBoardColumnRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.PostAsJsonAsync(
            $"api/boards/{boardId}/columns",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content
            .ReadFromJsonAsync<BoardColumnResponse>(
                cancellationToken);
    }

    public async Task<IReadOnlyList<BoardColumnResponse>?> GetAllAsync(
        Guid boardId,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.GetAsync(
            $"api/boards/{boardId}/columns",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content
            .ReadFromJsonAsync<IReadOnlyList<BoardColumnResponse>>(
                cancellationToken);
    }

    public async Task<BoardColumnResponse?> GetAsync(
        Guid boardId,
        Guid columnId,
        CancellationToken cancellationToken = default)
    {
        var columns = await GetAllAsync(
            boardId,
            cancellationToken);

        return columns?
            .FirstOrDefault(x => x.Id == columnId);
    }

    public async Task<bool> UpdateAsync(
        Guid boardId,
        Guid columnId,
        UpdateBoardColumnRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.PutAsJsonAsync(
            $"api/boards/{boardId}/columns/{columnId}",
            request,
            cancellationToken);

        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAsync(
        Guid boardId,
        Guid columnId,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.DeleteAsync(
            $"api/boards/{boardId}/columns/{columnId}",
            cancellationToken);

        return response.StatusCode == HttpStatusCode.NoContent;
    }
}