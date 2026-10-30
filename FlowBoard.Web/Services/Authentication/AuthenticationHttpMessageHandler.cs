using System.Net;
using System.Net.Http.Headers;

namespace FlowBoard.Web.Services.Authentication;

public class AuthenticationHttpMessageHandler
    : DelegatingHandler
{
    private readonly IAccessTokenProvider _accessTokenState;

    public AuthenticationHttpMessageHandler(
        IAccessTokenProvider accessTokenState)
    {
        _accessTokenState = accessTokenState;

        Console.WriteLine(
            "========== AUTH HTTP HANDLER CREATED ==========");

        Console.WriteLine(
 "========== AUTH HTTP HANDLER CREATED ==========");

        Console.WriteLine(
            $"AUTH HANDLER ACCESS TOKEN STATE INSTANCE = " +
            $"{_accessTokenState.GetInstanceId()}");
    }

    protected override async Task<HttpResponseMessage>
        SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
    {
        Console.WriteLine();
        Console.WriteLine(
            "========== AUTH HTTP HANDLER ==========");

        var accessToken =
            _accessTokenState.GetAccessToken();

        Console.WriteLine(
            $"AUTH HANDLER: Access token available = " +
            $"{!string.IsNullOrWhiteSpace(accessToken)}");

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    accessToken);

            Console.WriteLine(
                "AUTH HANDLER: Bearer access token attached.");
        }
        else
        {
            Console.WriteLine(
                "AUTH HANDLER: No access token available.");
        }

        Console.WriteLine(
            $"AUTH HANDLER: Sending " +
            $"{request.Method} {request.RequestUri}");

        var response =
            await base.SendAsync(
                request,
                cancellationToken);

        Console.WriteLine(
            $"AUTH HANDLER: Response status = " +
            $"{(int)response.StatusCode} {response.StatusCode}");

        Console.WriteLine(
            "========== AUTH HTTP HANDLER END ==========");

        return response;
    }
}