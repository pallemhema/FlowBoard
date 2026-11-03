namespace FlowBoard.Shared.DTOs.Auth;

public class AuthenticationResult
{
    public string AccessToken { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime RefreshTokenExpiresAt { get; set; }
}