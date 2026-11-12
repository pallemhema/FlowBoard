namespace FlowBoard.Shared.DTOs.Auth;

public class AuthenticationAttemptResult
{
    public AuthenticationResult? Authentication { get; set; }

    public string? ErrorMessage { get; set; }
}
