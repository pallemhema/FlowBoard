using System.Net.Http.Json;
using FlowBoard.Shared.DTOs.Cards;
using FlowBoard.Shared.DTOs.Common;

namespace FlowBoard.Web.Services.Api;

public class CardApiService : ICardApiService
{
    private readonly FlowBoardApiClient _apiClient;

    public CardApiService(
        FlowBoardApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    // ============================================================
    // GET CARDS BY COLUMN
    // ============================================================

    public async Task<PagedResponse<CardResponse>?> GetByColumnAsync(
        Guid columnId,
        CardQueryRequest request)
    {
        var url =
            $"api/Card/column/{columnId}" +
            $"?Page={request.Page}" +
            $"&PageSize={request.PageSize}";

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            url +=
                $"&Search={Uri.EscapeDataString(request.Search)}";
        }

        if (request.Priority.HasValue)
        {
            url +=
                $"&Priority={request.Priority.Value}";
        }

        if (request.AssignedUserId.HasValue)
        {
            url +=
                $"&AssignedUserId={request.AssignedUserId.Value}";
        }

        var response =
            await _apiClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content
            .ReadFromJsonAsync<PagedResponse<CardResponse>>();
    }

    // ============================================================
    // GET CARD
    // ============================================================

    public async Task<CardResponse?> GetByIdAsync(
        Guid cardId)
    {
        var response =
            await _apiClient.GetAsync(
                $"api/Card/{cardId}");

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content
            .ReadFromJsonAsync<CardResponse>();
    }

    // ============================================================
    // CREATE
    // ============================================================

    public async Task<CardResponse?> CreateAsync(
        Guid columnId,
        CreateCardRequest request)
    {
        var response =
            await _apiClient.PostAsJsonAsync(
                $"api/Card?boardColumnId={columnId}",
                request);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content
            .ReadFromJsonAsync<CardResponse>();
    }

    // ============================================================
    // UPDATE
    // ============================================================

    public async Task<bool> UpdateAsync(
        Guid cardId,
        UpdateCardRequest request)
    {
        var response =
            await _apiClient.PutAsJsonAsync(
                $"api/Card/{cardId}",
                request);

        return response.IsSuccessStatusCode;
    }

    // ============================================================
    // MOVE
    // ============================================================

    public async Task<bool> MoveAsync(
        Guid cardId,
        MoveCardRequest request)
    {
        var response =
            await _apiClient.PatchAsJsonAsync(
                $"api/Card/{cardId}/move",
                request);

        return response.IsSuccessStatusCode;
    }
    // ============================================================
    // DELETE
    // ============================================================

    public async Task<bool> DeleteAsync(
        Guid cardId)
    {
        var response =
            await _apiClient.DeleteAsync(
                $"api/Card/{cardId}");

        return response.IsSuccessStatusCode;
    }
}