using System.Net.Http.Json;
using FlowBoard.Shared.DTOs.CardComments;

namespace FlowBoard.Web.Services.Api;

public class CardCommentApiService : ICardCommentApiService
{
    private readonly FlowBoardApiClient _apiClient;

    public CardCommentApiService(
        FlowBoardApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    // ============================================================
    // GET COMMENTS
    // ============================================================

    public async Task<IReadOnlyList<CardCommentResponse>?> GetByCardAsync(
        Guid cardId)
    {
        var response =
            await _apiClient.GetAsync(
                $"api/CardComment/card/{cardId}");

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content
            .ReadFromJsonAsync<
                IReadOnlyList<CardCommentResponse>>();
    }

    // ============================================================
    // ADD COMMENT
    // ============================================================

    public async Task<CardCommentResponse?> AddAsync(
        Guid cardId,
        CreateCardCommentRequest request)
    {
        var response =
            await _apiClient.PostAsJsonAsync(
                $"api/CardComment/card/{cardId}",
                request);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content
            .ReadFromJsonAsync<CardCommentResponse>();
    }

    // ============================================================
    // UPDATE COMMENT
    // ============================================================

    public async Task<bool> UpdateAsync(
        Guid commentId,
        UpdateCardCommentRequest request)
    {
        var response =
            await _apiClient.PutAsJsonAsync(
                $"api/CardComment/{commentId}",
                request);

        return response.IsSuccessStatusCode;
    }

    // ============================================================
    // DELETE COMMENT
    // ============================================================

    public async Task<bool> DeleteAsync(
        Guid commentId)
    {
        var response =
            await _apiClient.DeleteAsync(
                $"api/CardComment/{commentId}");

        return response.IsSuccessStatusCode;
    }
}