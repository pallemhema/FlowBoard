using FlowBoard.Shared.DTOs.Auth;

namespace FlowBoard.Web.Services.Authentication;

public interface IAuthenticationService
{
    // ============================================================
    // ACCESS TOKEN
    // ============================================================

    string? AccessToken { get; }

    // ============================================================
    // INITIALIZE / RESTORE SESSION
    // ============================================================

    Task<bool> InitializeAsync(
        CancellationToken cancellationToken = default);

    // ============================================================
    // LOGIN
    // ============================================================

    Task<bool> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);

    // ============================================================
    // REFRESH
    // ============================================================

    Task<bool> RefreshAsync(
        CancellationToken cancellationToken = default);

    // ============================================================
    // LOGOUT
    // ============================================================

    Task LogoutAsync(
        CancellationToken cancellationToken = default);
}