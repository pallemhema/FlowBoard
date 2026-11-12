using Microsoft.JSInterop;
using FlowBoard.Shared.DTOs.Auth;
using FlowBoard.Shared.DTOs.Common;

namespace FlowBoard.Web.Services.Api;

public class AuthApiService : IAuthApiService
{
    private readonly HttpClient _backendClient;
    private readonly HttpClient _webClient;

    private readonly IJSRuntime _jsRuntime;

    public AuthApiService(
        IHttpClientFactory httpClientFactory,
        IJSRuntime jsRuntime)
    {
        _backendClient =
            httpClientFactory.CreateClient("FlowBoardAuthApi");

        _webClient =
            httpClientFactory.CreateClient("FlowBoardWeb");

        _jsRuntime = jsRuntime;

        Console.WriteLine(
            "========== AuthApiService CREATED ==========");
    }

    // ============================================================
    // LOGIN
    // Blazor/Web → Backend API
    // ============================================================
    // ============================================================
    // LOGIN
    // Blazor → Web AuthController
    // ============================================================

    public async Task<AuthenticationAttemptResult?> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine();
        Console.WriteLine(
            "========== AuthApiService: WEB LOGIN ==========");

        try
        {
            var result =
                await _jsRuntime.InvokeAsync<AuthenticationAttemptResult?>(
                    "flowBoardAuth.login",
                    cancellationToken,
                    request.Email,
                    request.Password);

            Console.WriteLine(
                $"WEB AuthApiService: Access token received = " +
                $"{!string.IsNullOrWhiteSpace(result?.Authentication?.AccessToken)}");

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"WEB AuthApiService: Browser login failed: {ex}");

            return null;
        }
    }


    // ============================================================
    // LOGIN BACKEND
    // Web AuthController → Backend API
    // ============================================================

    public async Task<AuthenticationAttemptResult> LoginBackendAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine();
        Console.WriteLine(
            "========== AuthApiService: BACKEND LOGIN ==========");

        Console.WriteLine(
            $"WEB AuthApiService: Calling backend /api/auth/login for {request.Email}");

        var response =
            await _backendClient.PostAsJsonAsync(
                "api/auth/login",
                request,
                cancellationToken);

        Console.WriteLine(
            $"WEB AuthApiService: Backend login response = " +
            $"{(int)response.StatusCode} {response.StatusCode}");

        Console.WriteLine($"Backend status: {(int)response.StatusCode}");

    var rawResponse = await response.Content.ReadAsStringAsync(
        cancellationToken);

    Console.WriteLine($"Backend error response: {rawResponse}");

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine(
                "WEB AuthApiService: Backend login FAILED.");

            var error = await response.Content
                .ReadFromJsonAsync<ErrorResponse>(
                    cancellationToken: cancellationToken);
            var errorMessage = error?.Message is
                AuthErrorMessages.InvalidCredentials or
                AuthErrorMessages.AccountDeactivated
                    ? error.Message
                    : AuthErrorMessages.InvalidCredentials;


                Console.WriteLine($"Deserialized error: {error?.Message}");

Console.WriteLine(
    $"Expected deactivated message: {AuthErrorMessages.AccountDeactivated}");

Console.WriteLine(
    $"Exact match: {error?.Message == AuthErrorMessages.AccountDeactivated}");

            return new AuthenticationAttemptResult
            {
                ErrorMessage = errorMessage
            };
        }

        var result =
            await response.Content.ReadFromJsonAsync<AuthenticationResult>(
                cancellationToken: cancellationToken);

        Console.WriteLine(
            $"WEB AuthApiService: Access token received = " +
            $"{!string.IsNullOrWhiteSpace(result?.AccessToken)}");

        Console.WriteLine(
            $"WEB AuthApiService: Refresh token received = " +
            $"{!string.IsNullOrWhiteSpace(result?.RefreshToken)}");

        return new AuthenticationAttemptResult
        {
            Authentication = result
        };
    }
    // ============================================================
    // REFRESH
    // Blazor → Web AuthController
    // ============================================================

    public async Task<AuthenticationResult?> RefreshAsync(
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine();
        Console.WriteLine(
            "========== AuthApiService: WEB REFRESH ==========");

        Console.WriteLine(
            "WEB AuthApiService: Calling browser /auth/refresh.");

        try
        {
            var result =
                await _jsRuntime.InvokeAsync<AuthenticationResult?>(
                    "flowBoardAuth.refresh");

            Console.WriteLine(
                $"WEB AuthApiService: Access token received = " +
                $"{!string.IsNullOrWhiteSpace(result?.AccessToken)}");

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"WEB AuthApiService: Browser refresh failed: {ex}");

            return null;
        }
    }

    public async Task<AuthenticationResult?> RefreshBackendAsync(
    string refreshToken,
    CancellationToken cancellationToken = default)
    {
        Console.WriteLine();
        Console.WriteLine(
            "========== AuthApiService: BACKEND REFRESH ==========");

        Console.WriteLine(
            "WEB AuthApiService: Calling backend /api/auth/refresh.");

        var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                "api/auth/refresh");

        request.Headers.Add(
            "Cookie",
            $"refreshToken={refreshToken}");

        var response =
            await _backendClient.SendAsync(
                request,
                cancellationToken);

        Console.WriteLine(
            $"WEB AuthApiService: Backend refresh response = " +
            $"{(int)response.StatusCode} {response.StatusCode}");

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine(
                "WEB AuthApiService: Backend refresh FAILED.");

            return null;
        }

        var result =
            await response.Content
                .ReadFromJsonAsync<AuthenticationResult>(
                    cancellationToken: cancellationToken);

        Console.WriteLine(
            $"WEB AuthApiService: New access token received = " +
            $"{!string.IsNullOrWhiteSpace(result?.AccessToken)}");

        Console.WriteLine(
            $"WEB AuthApiService: New refresh token received = " +
            $"{!string.IsNullOrWhiteSpace(result?.RefreshToken)}");

        return result;
    }

    // ============================================================
    // LOGOUT
    // Blazor → Web AuthController
    // ============================================================
    public async Task<bool> LogoutAsync(
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine();
        Console.WriteLine(
            "========== AuthApiService: WEB LOGOUT ==========");

        try
        {
            var success =
                await _jsRuntime.InvokeAsync<bool>(
                    "flowBoardAuth.logout");

            Console.WriteLine(
                $"WEB AuthApiService: Browser logout result = {success}");

            return success;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"WEB AuthApiService: Browser logout failed: {ex}");

            return false;
        }
    }
    public async Task<bool> LogoutBackendAsync(
    string refreshToken,
    CancellationToken cancellationToken = default)
    {
        Console.WriteLine();
        Console.WriteLine(
            "========== AuthApiService: BACKEND LOGOUT ==========");

        Console.WriteLine(
            "WEB AuthApiService: Calling backend /api/auth/logout.");

        var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                "api/auth/logout");

        request.Headers.Add(
            "Cookie",
            $"refreshToken={refreshToken}");

        var response =
            await _backendClient.SendAsync(
                request,
                cancellationToken);

        Console.WriteLine(
            $"WEB AuthApiService: Backend logout response = " +
            $"{(int)response.StatusCode} {response.StatusCode}");

        return response.IsSuccessStatusCode;
    }
}