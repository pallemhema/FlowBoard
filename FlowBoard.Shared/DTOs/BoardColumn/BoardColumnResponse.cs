namespace FlowBoard.Shared.DTOs.BoardColumns;

public class BoardColumnResponse
{
    public Guid Id { get; set; }

    public Guid BoardId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Position { get; set; }
}