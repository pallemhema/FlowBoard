namespace FlowBoard.Shared.DTOs.Boards;

using FlowBoard.Shared.Enums;

public class BoardResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Guid WorkspaceId { get; set; }

    public Guid? ProjectId { get; set; }

    public BoardPurpose Purpose { get; set; }

    public Guid OwnerId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ModifiedAt { get; set; }
}