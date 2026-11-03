using System.Text.Json;
using FlowBoard.Shared.DTOs.Auth;

namespace FlowBoard.Web.Services.Api;

public sealed class UserManagementApiService : IUserManagementApiService
{
    private readonly FlowBoardApiClient _apiClient;

    public UserManagementApiService(FlowBoardApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<CreateUserResponse> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _apiClient.PostAsJsonAsync(
            "api/auth/users",
            request,
            cancellationToken);

        return await ReadResponseAsync<CreateUserResponse>(
            response,
            "Account creation returned an empty response.",
            cancellationToken);
    }

    public async Task<CreateUserResponse> ReissueInvitationAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _apiClient.PostAsJsonAsync(
            $"api/auth/users/{userId}/invitation",
            new { },
            cancellationToken);

        return await ReadResponseAsync<CreateUserResponse>(
            response,
            "Invitation creation returned an empty response.",
            cancellationToken);
    }

    public async Task<PromoteUserResponse> PromoteEmployeeToManagerAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        using var response = await _apiClient.PostAsJsonAsync(
            $"api/auth/users/{userId}/promote-to-manager",
            new { },
            cancellationToken);

        return await ReadResponseAsync<PromoteUserResponse>(
            response,
            "The promotion returned an empty response.",
            cancellationToken);
    }

    public Task DeactivateUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        PostUserActionAsync(
            $"api/auth/users/{userId}/deactivate",
            "Account deactivation failed.",
            cancellationToken);

    public Task ReactivateUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        PostUserActionAsync(
            $"api/auth/users/{userId}/reactivate",
            "Account reactivation failed.",
            cancellationToken);

    public async Task ActivateInvitationAsync(
        ActivateInvitationRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await _apiClient.PostAsJsonAsync(
            "api/auth/invitations/activate",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw await CreateApiExceptionAsync(response, cancellationToken);
        }
    }

    private async Task PostUserActionAsync(
        string endpoint,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        using var response = await _apiClient.PostAsJsonAsync(
            endpoint,
            new { },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw await CreateApiExceptionAsync(response, cancellationToken);
        }

        if (response.StatusCode != System.Net.HttpStatusCode.NoContent)
        {
            throw new UserManagementApiException(
                errorMessage,
                response.StatusCode);
        }
    }

    private static async Task<TResponse> ReadResponseAsync<TResponse>(
        HttpResponseMessage response,
        string emptyResponseMessage,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateApiExceptionAsync(response, cancellationToken);
        }

        var result = await response.Content.ReadFromJsonAsync<TResponse>(
            cancellationToken);
        return result ?? throw new UserManagementApiException(
            emptyResponseMessage,
            response.StatusCode);
    }

    private static async Task<UserManagementApiException>
        CreateApiExceptionAsync(
            HttpResponseMessage response,
            CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var message = ReadErrorMessage(body) ??
                      $"The request failed with status {(int)response.StatusCode}.";

        return new UserManagementApiException(message, response.StatusCode);
    }

    private static string? ReadErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.TryGetProperty("message", out var message) &&
                message.ValueKind == JsonValueKind.String)
            {
                return message.GetString();
            }

            if (root.TryGetProperty("errors", out var errors) &&
                errors.ValueKind == JsonValueKind.Object)
            {
                var validationMessages = errors.EnumerateObject()
                    .SelectMany(property =>
                        property.Value.ValueKind == JsonValueKind.Array
                            ? property.Value.EnumerateArray()
                                .Where(value =>
                                    value.ValueKind == JsonValueKind.String)
                                .Select(value => value.GetString())
                            : []);

                var combined = string.Join(
                    " ",
                    validationMessages.Where(value =>
                        !string.IsNullOrWhiteSpace(value)));
                if (!string.IsNullOrWhiteSpace(combined))
                {
                    return combined;
                }
            }

            if (root.TryGetProperty("title", out var title) &&
                title.ValueKind == JsonValueKind.String)
            {
                return title.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }
}
