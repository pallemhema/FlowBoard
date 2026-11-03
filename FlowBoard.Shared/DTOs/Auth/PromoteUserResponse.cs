namespace FlowBoard.Shared.DTOs.Auth;

public class PromoteUserResponse
{
    public UserDirectoryResponse User { get; set; } = new();

    public string PreviousRole { get; set; } = string.Empty;

    public string NewRole { get; set; } = string.Empty;

    public DateTime ChangedAt { get; set; }
}
