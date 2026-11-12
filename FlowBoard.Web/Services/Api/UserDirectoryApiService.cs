using System.Net.Http.Json;
using FlowBoard.Shared.DTOs.Auth;
using FlowBoard.Shared.DTOs.Common;

namespace FlowBoard.Web.Services.Api;

public class UserDirectoryApiService : IUserDirectoryApiService
{
    private readonly FlowBoardApiClient _apiClient;

    public UserDirectoryApiService(FlowBoardApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<PagedResponse<UserDirectoryResponse>?> GetAsync(
        PagedSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var url = BuildUrl("api/auth/users", request);

        return await GetPagedAsync(url, cancellationToken);
    }

    public async Task<PagedResponse<UserDirectoryResponse>?> GetEmployeesAsync(
        PagedSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var url = BuildUrl("api/auth/employees", request);
        return await GetPagedAsync(url, cancellationToken);
    }

    private async Task<PagedResponse<UserDirectoryResponse>?> GetPagedAsync(
        string url,
        CancellationToken cancellationToken)
    {
        var response = await _apiClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content
            .ReadFromJsonAsync<PagedResponse<UserDirectoryResponse>>(
                cancellationToken);
    }

    private static string BuildUrl(
        string endpoint,
        PagedSearchRequest request)
    {
        var url = $"{endpoint}?Page={request.Page}&PageSize={request.PageSize}";

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            url += $"&Search={Uri.EscapeDataString(request.Search)}";
        }

        return url;
    }
}
