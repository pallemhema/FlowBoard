using FlowBoard.Shared.DTOs.Auth;

namespace FlowBoard.Web.Services.Api;

public interface IAuthApiService
{
    // ============================================================
    // LOGIN
    // ============================================================

    Task<AuthenticationResult?> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthenticationResult?> LoginBackendAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);


    // ============================================================
    // REGISTER
    // ============================================================

    Task<AuthenticationResult?> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default);




    Task<AuthenticationResult?> RegisterBackendAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default);


    // ============================================================
    // REFRESH
    // ============================================================

    Task<AuthenticationResult?> RefreshAsync(
        CancellationToken cancellationToken = default);

    Task<AuthenticationResult?> RefreshBackendAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);


    // ============================================================
    // LOGOUT
    // ============================================================

    Task<bool> LogoutAsync(
        CancellationToken cancellationToken = default);

    Task<bool> LogoutBackendAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);
}