using FlowBoard.Shared.DTOs.Auth;
using FlowBoard.Shared.DTOs.Common;
using FlowBoard.Web.Configuration;
using FlowBoard.Web.Services.Api;
using Microsoft.AspNetCore.Mvc;

namespace FlowBoard.Web.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthApiService _authApiService;
    private readonly IHostEnvironment _environment;

    public AuthController(
        IAuthApiService authApiService,
        IHostEnvironment environment)
    {
        _authApiService = authApiService;
        _environment = environment;

        Console.WriteLine(
            "========== WEB AuthController CREATED ==========");
    }

    // ============================================================
    // LOGIN
    // Browser → Web → Backend
    // ============================================================

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        Console.WriteLine();
        Console.WriteLine(
            "========== WEB LOGIN START ==========");

        Console.WriteLine(
            $"WEB: Login request received for {request.Email}");

        var result =
            await _authApiService.LoginBackendAsync(
                request,
                cancellationToken);

        if (result.Authentication is null)
        {
            Console.WriteLine(
                "WEB: Backend login FAILED.");

            return Unauthorized(new ErrorResponse
            {
                Code = "UNAUTHORIZED",
                Message = result.ErrorMessage ??
                    AuthErrorMessages.InvalidCredentials
            });
        }

        var authentication = result.Authentication;

        Console.WriteLine(
            "WEB: Backend login SUCCESS.");

        Console.WriteLine(
            $"WEB: Access token received = " +
            $"{!string.IsNullOrWhiteSpace(authentication.AccessToken)}");

        Console.WriteLine(
            $"WEB: Refresh token received = " +
            $"{!string.IsNullOrWhiteSpace(authentication.RefreshToken)}");

        SetRefreshTokenCookie(
            authentication.RefreshToken,
            authentication.RefreshTokenExpiresAt);

        Console.WriteLine(
            "WEB: Refresh token cookie set.");

        return Ok(CreateAuthenticationResponse(authentication));
    }

    // ============================================================
    // REFRESH
    // ============================================================


    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(
        CancellationToken cancellationToken)
    {
        Console.WriteLine();
        Console.WriteLine(
            "========== WEB AUTH CONTROLLER: REFRESH ==========");

        // 1. Read refresh token from HttpOnly cookie

        var refreshToken =
            Request.Cookies["refreshToken"];

        Console.WriteLine(
            $"WEB: Refresh token cookie found = " +
            $"{!string.IsNullOrWhiteSpace(refreshToken)}");

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            Console.WriteLine(
                "WEB: Refresh token cookie NOT FOUND.");

            return Unauthorized();
        }

        // 2. Call backend API

        Console.WriteLine(
            "WEB: Calling backend /api/auth/refresh.");

        var result =
            await _authApiService.RefreshBackendAsync(
                refreshToken,
                cancellationToken);

        if (result is null)
        {
            Console.WriteLine(
                "WEB: Backend refresh FAILED.");

            Response.Cookies.Delete(
                RefreshTokenCookieOptions.Name,
                RefreshTokenCookieOptions.CreateForDeletion(
                    _environment,
                    Request.IsHttps));

            return Unauthorized();
        }

        Console.WriteLine(
            "WEB: Backend refresh SUCCESS.");

        // 3. Replace refresh token cookie

        if (!string.IsNullOrWhiteSpace(
                result.RefreshToken))
        {
            Response.Cookies.Append(
                RefreshTokenCookieOptions.Name,
                result.RefreshToken,
                RefreshTokenCookieOptions.Create(
                    _environment,
                    Request.IsHttps,
                    expiresAt: new DateTimeOffset(
                        result.RefreshTokenExpiresAt,
                        TimeSpan.Zero)));

            Console.WriteLine(
                "WEB: New refresh token cookie set.");
        }

        Console.WriteLine(
            "========== WEB REFRESH COMPLETE ==========");

        // 4. Return new access token to Blazor

        return Ok(CreateAuthenticationResponse(result));
    }

    // ============================================================
    // LOGOUT
    // Browser → Web → Backend
    // ============================================================

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        CancellationToken cancellationToken)
    {
        Console.WriteLine();
        Console.WriteLine(
            "========== WEB AUTH CONTROLLER: LOGOUT ==========");

        // 1. Read refresh token from cookie

        var refreshToken =
            Request.Cookies["refreshToken"];

        Console.WriteLine(
            $"WEB: Refresh token cookie found = " +
            $"{!string.IsNullOrWhiteSpace(refreshToken)}");

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            // 2. Tell backend to invalidate refresh token

            Console.WriteLine(
                "WEB: Calling backend /api/auth/logout.");

            var backendLogoutResult =
                await _authApiService.LogoutBackendAsync(
                    refreshToken,
                    cancellationToken);

            Console.WriteLine(
                $"WEB: Backend logout result = " +
                $"{backendLogoutResult}");
        }

        // 3. Delete browser cookie

        Response.Cookies.Delete(
            RefreshTokenCookieOptions.Name,
            RefreshTokenCookieOptions.CreateForDeletion(
                _environment,
                Request.IsHttps));

        Console.WriteLine(
            "WEB: Refresh token cookie deleted.");

        Console.WriteLine(
            "========== WEB LOGOUT COMPLETE ==========");

        return Ok();
    }

    // ============================================================
    // COOKIE
    // ============================================================

    private void SetRefreshTokenCookie(
        string refreshToken,
        DateTime refreshTokenExpiresAt)
    {
        Console.WriteLine(
            "WEB: Creating HttpOnly refresh token cookie.");

        Response.Cookies.Append(
            RefreshTokenCookieOptions.Name,
            refreshToken,
            RefreshTokenCookieOptions.Create(
                _environment,
                Request.IsHttps,
                expiresAt: new DateTimeOffset(
                    refreshTokenExpiresAt,
                    TimeSpan.Zero)));

        Console.WriteLine(
            $"WEB: Cookie '{RefreshTokenCookieOptions.Name}' appended.");

        Console.WriteLine(
            "WEB: HttpOnly = true");

        Console.WriteLine(
            "WEB: Refresh token is NOT exposed to JavaScript.");
    }

    private static AuthenticationResult CreateAuthenticationResponse(
        AuthenticationResult result)
    {
        return new AuthenticationResult
        {
            AccessToken = result.AccessToken,
            ExpiresAt = result.ExpiresAt
        };
    }
}