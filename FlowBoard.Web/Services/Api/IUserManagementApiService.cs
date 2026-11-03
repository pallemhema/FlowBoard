using FlowBoard.Shared.DTOs.Auth;

namespace FlowBoard.Web.Services.Api;

public interface IUserManagementApiService
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
}
