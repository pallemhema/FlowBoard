namespace FlowBoard.Api.Services.Authentication;

public interface ITokenService
{
    string GenerateAccessToken(
        Guid userId,
        string email,
        string role);

    string GenerateRefreshToken();
}