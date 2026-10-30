namespace FlowBoard.Shared.DTOs.Auth;

public class UserProfileResponse
{
    public Guid UserId { get; set; }

    public string? Email { get; set; }

    public string? Role { get; set; }
}