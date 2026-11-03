using FlowBoard.Shared.DTOs.Auth;
using FlowBoard.Shared.DTOs.Common;

namespace FlowBoard.Api.Services.Authentication;

public interface IAuthService
{
    Task<CreateUserResponse> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default);

    Task<CreateUserResponse> ReissueInvitationAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<PromoteUserResponse> PromoteEmployeeToManagerAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task DeactivateUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task ReactivateUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task ActivateInvitationAsync(
        ActivateInvitationRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthenticationResult> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthenticationResult> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    Task LogoutAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    Task<ProfileResponse?> GetProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<UserDirectoryResponse>> GetUsersAsync(
        PagedSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<ProfileResponse?> UpdateProfileAsync(
Guid userId,
UpdateProfileRequest request,
CancellationToken cancellationToken = default);
}
