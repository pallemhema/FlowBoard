using System.Net;
using System.Net.Http.Headers;
using FlowBoard.Web.Services.Authentication;

namespace FlowBoard.Web.Services.Api;

public class FlowBoardApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IAccessTokenProvider _accessTokenState;
    private readonly IAuthenticationService _authenticationService;

    // Only one request is allowed to perform token refresh.
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public FlowBoardApiClient(
        IHttpClientFactory httpClientFactory,
        IAccessTokenProvider accessTokenState,
        IAuthenticationService authenticationService)
    {
        _httpClient =
            httpClientFactory.CreateClient("FlowBoardApi");

        _accessTokenState =
            accessTokenState;

        _authenticationService =
            authenticationService;

        Console.WriteLine(
            "========== FlowBoardApiClient CREATED ==========");

        Console.WriteLine(
            $"FLOWBOARD API CLIENT TOKEN STATE = " +
            $"{_accessTokenState.GetInstanceId()}");
    }

    private void AttachAccessToken(
        HttpRequestMessage request)
    {
        var accessToken =
            _accessTokenState.GetAccessToken();

        Console.WriteLine();
        Console.WriteLine(
            "========== FLOWBOARD API CLIENT ==========");

        Console.WriteLine(
            $"API CLIENT TOKEN STATE = " +
            $"{_accessTokenState.GetInstanceId()}");

        Console.WriteLine(
            $"API CLIENT: Access token available = " +
            $"{!string.IsNullOrWhiteSpace(accessToken)}");

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            Console.WriteLine(
                "API CLIENT: No access token available.");

            return;
        }

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                accessToken);

        Console.WriteLine(
            "API CLIENT: Bearer token attached.");

        Console.WriteLine(
            "===========================================");
    }

    public async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        // ============================================================
        // FIRST REQUEST
        // ============================================================

        var retryRequest =
            await CloneRequestAsync(
                request,
                cancellationToken);

        AttachAccessToken(request);

        var response =
            await _httpClient.SendAsync(
                request,
                cancellationToken);

        // ============================================================
        // REQUEST SUCCESSFUL
        // ============================================================

        if (response.StatusCode !=
            HttpStatusCode.Unauthorized)
        {
            return response;
        }

        Console.WriteLine();
        Console.WriteLine(
            "========== FLOWBOARD API CLIENT: 401 ==========");

        Console.WriteLine(
            "API CLIENT: Access token was rejected.");

        // ============================================================
        // REFRESH LOCK
        // ============================================================

        await _refreshLock.WaitAsync(
            cancellationToken);

        try
        {
            Console.WriteLine(
                "API CLIENT: Entered refresh lock.");

            // ========================================================
            // IMPORTANT:
            //
            // Another request may already have refreshed the token
            // while this request was waiting for the lock.
            //
            // Therefore we check whether the token changed before
            // performing another refresh.
            // ========================================================

            var currentToken =
                _accessTokenState.GetAccessToken();

            var requestToken =
                request.Headers.Authorization?.Parameter;

            if (!string.IsNullOrWhiteSpace(currentToken) &&
                currentToken != requestToken)
            {
                Console.WriteLine(
                    "API CLIENT: Token was already refreshed by another request.");

                Console.WriteLine(
                    "API CLIENT: Skipping duplicate refresh.");
            }
            else
            {
                // ====================================================
                // WE ARE THE REQUEST RESPONSIBLE FOR REFRESHING
                // ====================================================

                Console.WriteLine(
                    "API CLIENT: Performing token refresh.");

                var refreshSuccessful =
                    await _authenticationService.RefreshAsync();

                if (!refreshSuccessful)
                {
                    Console.WriteLine(
                        "API CLIENT: Token refresh failed.");

                    return response;
                }

                Console.WriteLine(
                    "API CLIENT: Token refresh successful.");
            }
        }
        finally
        {
            _refreshLock.Release();

            Console.WriteLine(
                "API CLIENT: Refresh lock released.");
        }

        // ============================================================
        // ORIGINAL RESPONSE NO LONGER NEEDED
        // ============================================================

        response.Dispose();

        // ============================================================
        // RETRY ORIGINAL REQUEST
        // ============================================================

        Console.WriteLine(
            "API CLIENT: Retrying original request.");

        AttachAccessToken(retryRequest);

        var retryResponse =
            await _httpClient.SendAsync(
                retryRequest,
                cancellationToken);

        Console.WriteLine(
            $"API CLIENT: Retry response = " +
            $"{(int)retryResponse.StatusCode} " +
            $"{retryResponse.StatusCode}");

        Console.WriteLine(
            "================================================");

        return retryResponse;
    }

    public async Task<HttpResponseMessage> GetAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                url);

        return await SendAsync(
            request,
            cancellationToken);
    }

    public async Task<HttpResponseMessage> PutAsJsonAsync<T>(
        string url,
        T value,
        CancellationToken cancellationToken = default)
    {
        var request =
            new HttpRequestMessage(
                HttpMethod.Put,
                url);

        request.Content =
            JsonContent.Create(value);

        return await SendAsync(
            request,
            cancellationToken);
    }

    public async Task<HttpResponseMessage> PostAsJsonAsync<T>(
        string url,
        T value,
        CancellationToken cancellationToken = default)
    {
        var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                url);

        request.Content =
            JsonContent.Create(value);

        return await SendAsync(
            request,
            cancellationToken);
    }
    public async Task<HttpResponseMessage> PatchAsJsonAsync<T>(
    string url,
    T value,
    CancellationToken cancellationToken = default)
    {
        var request =
            new HttpRequestMessage(
                HttpMethod.Patch,
                url);

        request.Content =
            JsonContent.Create(value);

        return await SendAsync(
            request,
            cancellationToken);
    }

    public async Task<HttpResponseMessage> DeleteAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        var request =
            new HttpRequestMessage(
                HttpMethod.Delete,
                url);

        return await SendAsync(
            request,
            cancellationToken);
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var clone =
            new HttpRequestMessage(
                request.Method,
                request.RequestUri);

        // Copy request headers.
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(
                header.Key,
                header.Value);
        }

        // Copy request content.
        if (request.Content is not null)
        {
            var contentBytes =
                await request.Content.ReadAsByteArrayAsync(
                    cancellationToken);

            clone.Content =
                new ByteArrayContent(contentBytes);

            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(
                    header.Key,
                    header.Value);
            }
        }

        return clone;
    }
}