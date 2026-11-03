using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlowBoard.Shared.DTOs.BoardMembers;
using FlowBoard.Shared.DTOs.Common;
using FlowBoard.Web.Services.UI;

namespace FlowBoard.Web.Services.Api;

public class BoardMemberApiService : IBoardMemberApiService
{
    private readonly FlowBoardApiClient _apiClient;
    private readonly ToastService _toastService;

    public BoardMemberApiService(
        FlowBoardApiClient apiClient,
        ToastService toastService)
    {
        _apiClient = apiClient;
        _toastService = toastService;
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

        var response = await _apiClient.GetAsync(url);
        return await ReadAsync<PagedResponse<BoardMemberResponse>>(
            response,
            "Unable to load board members.");
    }

    // ============================================================
    // ADD MEMBER
    // ============================================================

    public async Task<BoardMemberResponse?> AddAsync(
        Guid boardId,
        AddBoardMemberRequest request)
    {
        var response = await _apiClient.PostAsJsonAsync(
            $"api/boards/{boardId}/members",
            request);
        var member = await ReadAsync<BoardMemberResponse>(
            response,
            "Unable to add the board member.",
            "Only eligible Employee accounts can be added. They must already belong to the associated project or workspace.",
            "The user is already a member of this board.");
        if (member is not null)
        {
            _toastService.Success("Board member added.");
        }

        return member;
    }

    // ============================================================
    // UPDATE MEMBER ROLE
    // ============================================================

    public async Task<bool> UpdateRoleAsync(
        Guid boardId,
        Guid userId,
        UpdateBoardMemberRoleRequest request)
    {
        var response = await _apiClient.PutAsJsonAsync(
            $"api/boards/{boardId}/members/{userId}/role",
            request);
        var updated = await ReportSuccessAsync(
            response,
            "Unable to update the board member role.",
            "The Owner role cannot be assigned to a board member.");
        if (updated)
        {
            _toastService.Success("Board member role updated.");
        }

        return updated;
    }

    // ============================================================
    // DELETE MEMBER
    // ============================================================

    public async Task<bool> DeleteAsync(
        Guid boardId,
        Guid userId)
    {
        var response = await _apiClient.DeleteAsync(
            $"api/boards/{boardId}/members/{userId}");
        var removed = await ReportSuccessAsync(
            response,
            "Unable to remove the board member.",
            "This member cannot be removed while task assignment dependencies remain.");
        if (removed)
        {
            _toastService.Success("Board member removed.");
        }

        return removed;
    }

    private async Task<T?> ReadAsync<T>(
        HttpResponseMessage response,
        string errorMessage,
        string? validationMessage = null,
        string? conflictMessage = null)
    {
        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                await ReportFailureAsync(
                    response,
                    errorMessage,
                    validationMessage,
                    conflictMessage);
                return default;
            }

            return await response.Content.ReadFromJsonAsync<T>();
        }
    }

    private async Task<bool> ReportSuccessAsync(
        HttpResponseMessage response,
        string errorMessage,
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
                conflictMessage: conflictMessage);
            return false;
        }
    }

    private async Task ReportFailureAsync(
        HttpResponseMessage response,
        string errorMessage,
        string? validationMessage = null,
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
                .ReadFromJsonAsync<ErrorResponse>())?.Message;
        }
        catch (JsonException)
        {
        }

        var fallbackMessage = response.StatusCode switch
        {
            HttpStatusCode.Forbidden => "You are not authorized to manage members of this board.",
            HttpStatusCode.NotFound => "The board or user could not be found.",
            HttpStatusCode.Conflict => conflictMessage ?? "This operation conflicts with current board membership or task assignments.",
            HttpStatusCode.BadRequest => validationMessage ?? "The board member request is invalid.",
            _ => errorMessage
        };

        _toastService.Error(
            string.IsNullOrWhiteSpace(apiMessage) ? fallbackMessage : apiMessage);
    }
}