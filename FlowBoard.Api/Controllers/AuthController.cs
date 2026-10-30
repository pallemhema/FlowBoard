using FlowBoard.Api.Services.Authentication;
using FlowBoard.Api.Authorization;
using FlowBoard.Shared.DTOs.Auth;
using FlowBoard.Shared.DTOs.Common;
using FlowBoard.Api.Services.CurrentUser;
using FlowBoard.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowBoard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    private readonly ICurrentUserService _currentUserService;

    public AuthController(
        IAuthService authService,
        ICurrentUserService currentUserService)
    {
        _authService = authService;
        _currentUserService = currentUserService;
    }

    // ============================================================
    // REGISTER
    // ============================================================

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthenticationResult>> Register(
       RegisterRequest request,
       CancellationToken cancellationToken)
    {
        var result = await _authService.RegisterAsync(
            request,
            cancellationToken);

        return Ok(result);
    }

    // ============================================================
    // LOGIN
    // ============================================================
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthenticationResult>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(
            request,
            cancellationToken);

        return Ok(result);
    }

    // ============================================================
    // REFRESH
    // ============================================================
    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthenticationResult>> Refresh(
        CancellationToken cancellationToken)
    {
        Console.WriteLine("========== API REFRESH ==========");

        if (!Request.Cookies.TryGetValue(
                "refreshToken",
                out var refreshToken))
        {
            Console.WriteLine(
                "API: Refresh token cookie NOT FOUND.");

            return Unauthorized();
        }

        Console.WriteLine(
            "API: Refresh token cookie FOUND.");

        var result =
            await _authService.RefreshTokenAsync(
                refreshToken,
                cancellationToken);

        if (result is null)
        {
            return Unauthorized();
        }

        return Ok(result);
    }
    // ============================================================
    // LOGOUT
    // ============================================================
    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        CancellationToken cancellationToken)
    {
        Console.WriteLine("========== API LOGOUT ==========");

        if (Request.Cookies.TryGetValue(
                "refreshToken",
                out var refreshToken))
        {
            await _authService.LogoutAsync(
                refreshToken,
                cancellationToken);
        }

        return NoContent();
    }
    [Authorize(Policy = AuthorizationPolicies.AuthenticatedUser)]
    [HttpGet("profile")]
    public async Task<ActionResult<ProfileResponse>> Me(
        CancellationToken cancellationToken)
    {
        Console.WriteLine(
            "========== API PROFILE ==========");

        var userId = _currentUserService.UserId;

        Console.WriteLine(
            $"API: Current User ID = {userId}");

        var profile = await _authService.GetProfileAsync(
            userId,
            cancellationToken);

        if (profile is null)
        {
            Console.WriteLine(
                "API: User profile NOT FOUND.");

            return NotFound();
        }

        Console.WriteLine(
            "API: User profile FOUND.");

        return Ok(profile);
    }

    [Authorize(Policy = AuthorizationPolicies.AdminOrManager)]
    [HttpGet("users")]
    public async Task<ActionResult<PagedResponse<UserDirectoryResponse>>> GetUsers(
        [FromQuery] PagedSearchRequest request,
        CancellationToken cancellationToken)
    {
        var users = await _authService.GetUsersAsync(
            request,
            cancellationToken);

        return Ok(users);
    }

    [Authorize(Policy = AuthorizationPolicies.AuthenticatedUser)]
    [HttpPut("profile")]
    public async Task<ActionResult<ProfileResponse>> UpdateProfile(
    UpdateProfileRequest request,
    CancellationToken cancellationToken)
    {
        Console.WriteLine(
            "========== API UPDATE PROFILE ==========");

        var userId = _currentUserService.UserId;

        Console.WriteLine(
            $"API: Current User ID = {userId}");

        var profile = await _authService.UpdateProfileAsync(
            userId,
            request,
            cancellationToken);

        if (profile is null)
        {
            Console.WriteLine(
                "API: User profile NOT FOUND.");

            return NotFound();
        }

        Console.WriteLine(
            "API: User profile UPDATED.");

        return Ok(profile);
    }
}