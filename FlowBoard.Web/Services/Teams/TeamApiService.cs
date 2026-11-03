using System.Net;
using System.Net.Http.Json;
using FlowBoard.Shared.DTOs.Common;
using FlowBoard.Shared.DTOs.Teams;
using FlowBoard.Web.Services.UI;

namespace FlowBoard.Web.Services.Api;

public sealed class TeamApiService : ITeamApiService
{
    private const string Root = "api/teams";
    private readonly FlowBoardApiClient _apiClient;
    private readonly ToastService _toastService;

    public TeamApiService(
        FlowBoardApiClient apiClient,
        ToastService toastService)
    {
        _apiClient = apiClient;
        _toastService = toastService;
    }

    public async Task<TeamResponse?> CreateAsync(
        CreateTeamRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.PostAsJsonAsync(
            Root,
            request,
            cancellationToken);
        return await ReadAsync<TeamResponse>(
            response,
            "Unable to create the team.",
            cancellationToken);
    }

    public Task<PagedResponse<TeamResponse>?> GetAllAsync(
        PagedSearchRequest request,
        CancellationToken cancellationToken = default) =>
        GetPageAsync<TeamResponse>($"{Root}{PageQuery(request)}", cancellationToken);

    public Task<PagedResponse<TeamResponse>?> GetMineAsync(
        PagedSearchRequest request,
        CancellationToken cancellationToken = default) =>
        GetPageAsync<TeamResponse>($"{Root}/mine{PageQuery(request)}", cancellationToken);

    public async Task<TeamResponse?> GetByIdAsync(
        Guid teamId,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.GetAsync(
            $"{Root}/{teamId}",
            cancellationToken);
        return await ReadAsync<TeamResponse>(
            response,
            "Unable to load the team.",
            cancellationToken);
    }

    public async Task<TeamResponse?> UpdateAsync(
        Guid teamId,
        UpdateTeamRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.PutAsJsonAsync(
            $"{Root}/{teamId}",
            request,
            cancellationToken);
        return await ReadAsync<TeamResponse>(
            response,
            "Unable to update the team.",
            cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        Guid teamId,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.DeleteAsync(
            $"{Root}/{teamId}",
            cancellationToken);
        return ReportSuccess(
            response,
            "Unable to delete the team.",
            conflictMessage: "This team still has members. Remove its members before deleting the team.");
    }

    public Task<PagedResponse<TeamMemberResponse>?> GetMembersAsync(
        Guid teamId,
        PagedSearchRequest request,
        CancellationToken cancellationToken = default) =>
        GetPageAsync<TeamMemberResponse>(
            $"{Root}/{teamId}/members{PageQuery(request)}",
            cancellationToken);

    public async Task<TeamMemberResponse?> AddMemberAsync(
        Guid teamId,
        AddTeamMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.PostAsJsonAsync(
            $"{Root}/{teamId}/members",
            request,
            cancellationToken);
        return await ReadAsync<TeamMemberResponse>(
            response,
            "Unable to add the team member.",
            cancellationToken,
            conflictMessage: "This account is already a member of the team.",
            validationMessage: "Only Employee accounts can be added to a team.");
    }

    public async Task<bool> RemoveMemberAsync(
        Guid teamId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.DeleteAsync(
            $"{Root}/{teamId}/members/{userId}",
            cancellationToken);
        return ReportSuccess(response, "Unable to remove the team member.");
    }

    private async Task<PagedResponse<T>?> GetPageAsync<T>(
        string url,
        CancellationToken cancellationToken)
    {
        var response = await _apiClient.GetAsync(url, cancellationToken);
        return await ReadAsync<PagedResponse<T>>(
            response,
            "Unable to load the teams.",
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
                ReportFailure(
                    response.StatusCode,
                    errorMessage,
                    conflictMessage,
                    validationMessage);
                return default;
            }

            return await response.Content.ReadFromJsonAsync<T>(
                cancellationToken);
        }
    }

    private bool ReportSuccess(
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

            ReportFailure(
                response.StatusCode,
                errorMessage,
                conflictMessage);
            return false;
        }
    }

    private void ReportFailure(
        HttpStatusCode statusCode,
        string errorMessage,
        string? conflictMessage = null,
        string? validationMessage = null)
    {
        var message = statusCode switch
        {
            HttpStatusCode.Unauthorized => null,
            HttpStatusCode.Forbidden => "You are not authorized to perform this team operation.",
            HttpStatusCode.NotFound => "The team or account could not be found. Refresh the page and try again.",
            HttpStatusCode.Conflict => conflictMessage
                ?? "This team operation conflicts with the current team data.",
            HttpStatusCode.BadRequest => validationMessage
                ?? "The team request is invalid. Check the entered information.",
            _ => errorMessage
        };

        if (message is not null)
        {
            _toastService.Error(message);
        }
    }

    private static string PageQuery(PagedSearchRequest request)
    {
        var query = $"?Page={request.Page}&PageSize={request.PageSize}";
        return string.IsNullOrWhiteSpace(request.Search)
            ? query
            : $"{query}&Search={Uri.EscapeDataString(request.Search)}";
    }
}
