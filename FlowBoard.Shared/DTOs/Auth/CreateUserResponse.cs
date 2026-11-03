namespace FlowBoard.Shared.DTOs.Auth;

public class CreateUserResponse
{
    public UserDirectoryResponse User { get; set; } = new();

    public string InvitationToken { get; set; } = string.Empty;

    public DateTime InvitationExpiresAt { get; set; }
}
