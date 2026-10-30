namespace FlowBoard.Shared.DTOs.Teams;

public class TeamResponse
{
    public Guid TeamId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int MemberCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ModifiedAt { get; set; }
}
