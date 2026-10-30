using System.Net.Http.Json;

using FlowBoard.Shared.DTOs.BoardMembers;
using FlowBoard.Shared.DTOs.Common;

namespace FlowBoard.Web.Services.Api;

public class BoardMemberApiService : IBoardMemberApiService
{
    private readonly FlowBoardApiClient _apiClient;

    public BoardMemberApiService(
        FlowBoardApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    // ============================================================
    // GET MEMBERS
    // ============================================================

    public async Task<PagedResponse<BoardMemberResponse>?> GetAsync(
        Guid boardId,
        PagedSearchRequest request)
    {
        var url =
            $"api/boards/{boardId}/members" +
            $"?Page={request.Page}" +
            $"&PageSize={request.PageSize}";

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            url +=
                $"&Search={Uri.EscapeDataString(request.Search)}";
        }

        var response =
            await _apiClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content
            .ReadFromJsonAsync<PagedResponse<BoardMemberResponse>>();
    }

    // ============================================================
    // ADD MEMBER
    // ============================================================

    public async Task<BoardMemberResponse?> AddAsync(
        Guid boardId,
        AddBoardMemberRequest request)
    {
        var response =
            await _apiClient.PostAsJsonAsync(
                $"api/boards/{boardId}/members",
                request);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content
            .ReadFromJsonAsync<BoardMemberResponse>();
    }

    // ============================================================
    // UPDATE MEMBER ROLE
    // ============================================================

    public async Task<bool> UpdateRoleAsync(
        Guid boardId,
        Guid userId,
        UpdateBoardMemberRoleRequest request)
    {
        var response =
            await _apiClient.PutAsJsonAsync(
                $"api/boards/{boardId}/members/{userId}/role",
                request);

        return response.IsSuccessStatusCode;
    }

    // ============================================================
    // DELETE MEMBER
    // ============================================================

    public async Task<bool> DeleteAsync(
        Guid boardId,
        Guid userId)
    {
        var response =
            await _apiClient.DeleteAsync(
                $"api/boards/{boardId}/members/{userId}");

        return response.IsSuccessStatusCode;
    }
}